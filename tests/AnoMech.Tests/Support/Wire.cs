namespace AnoMech.Tests;

// One TCP connection between a client and the relay, as the client's link carries it: what either
// end writes reaches the other in order, each write whole. A read takes from one write at most,
// so what an end reads next is always known as part of the write it came in (tags). Every read and
// write completes on the test's thread, when the session's time reaches it (Step), except the
// relay's quiet close (see VirtualClock), which reads from the thread pool on a connection it ends.
//
// Up is the client's upload: a socket buffer of SendBufferBytes, drained at UploadRate, each
// write arriving UploadLatency after its last byte left. Down is the relay's sending to it:
// DownloadRate, DownloadLatency and DownloadJitter per write, in order. Stalls hold back whatever
// is behind them. Cut is the link going down: whatever it carries is lost and the relay's end
// never hears of it. An end closing reaches the other end behind its data.
internal sealed class Wire
{
    // Per write: the TCP/IP and TLS framing around it.
    private const int Overhead = 32;

    private readonly ConnectionProfile link;
    private readonly Func<double> now;
    private readonly Func<double, bool> uploadsDead, downloadsDead, uploadsStalled;
    private readonly Random swing, jitter;
    private readonly LinkStalls uploadStalls, downloadStalls;

    private readonly Queue<Write> waiting = new(), sendBuffer = new();
    private readonly Queue<(double At, Write Write)> goingUp = new(), goingDown = new();
    private int buffered;
    private double uploadCredit, uploadShare = 1, nextSwing, downloadFreeAt, lastArrival;
    private bool cut;

    public End ClientEnd { get; }
    public End RelayEnd { get; }
    public bool Idle => waiting.Count == 0 && sendBuffer.Count == 0 && goingUp.Count == 0 && goingDown.Count == 0;

    // relayRead: the tag of what the relay's end just read. relayWriting: the tag for what it
    // writes, which the relay forwards from whichever connection it read last.
    public Wire(ConnectionProfile link, int seed, Func<double> now, Func<double, bool> uploadsDead, Func<double, bool> downloadsDead,
        Func<double, bool> uploadsStalled, Action<object?> relayRead, Func<object?> relayWriting)
    {
        this.link = link;
        this.now = now;
        this.uploadsDead = uploadsDead;
        this.downloadsDead = downloadsDead;
        this.uploadsStalled = uploadsStalled;
        swing = new Random(seed + 1);
        jitter = new Random(seed + 2);
        uploadStalls = new LinkStalls(new Random(seed + 3), link.UploadStallEvery, link.UploadStallMin, link.UploadStallMax);
        downloadStalls = new LinkStalls(new Random(seed + 4), link.DownloadStallEvery, link.DownloadStallMin, link.DownloadStallMax);
        ClientEnd = new End(this, client: true, _ => { }, () => null);
        RelayEnd = new End(this, client: false, relayRead, relayWriting);
    }

    // A write, or an end's close (Bytes null) behind the writes before it.
    internal sealed class Write(byte[]? bytes, object? tag)
    {
        public readonly byte[]? Bytes = bytes;
        public readonly object? Tag = tag;
        public readonly TaskCompletionSource Accepted = new();
        public int Unaccepted = (bytes?.Length ?? 0) + Overhead;
        public int Untransmitted;
        public bool Buffered;
        public int Read;
        public bool IsClose => Bytes == null;
    }

    public void Cut()
    {
        if (cut) return;
        cut = true;
        foreach (var write in waiting) write.Accepted.TrySetException(new IOException("The link went down."));
        waiting.Clear();
        sendBuffer.Clear();
        goingUp.Clear();
        goingDown.Clear();
        buffered = 0;
        ClientEnd.Lost();
    }

    public void Step(double step)
    {
        var at = now();
        Accept();
        if (at >= nextSwing)
        {
            uploadShare = 1 - link.UploadSwing + swing.NextDouble() * 2 * link.UploadSwing;
            nextSwing = at + link.UploadSwingEvery;
        }
        if (uploadStalls.Within(at) == null && !uploadsStalled(at))
        {
            uploadCredit += Math.Min(link.UploadRate * uploadShare * step, int.MaxValue);
            while (sendBuffer.Count > 0 && uploadCredit >= 1)
            {
                var head = sendBuffer.Peek();
                var bytes = (int)Math.Min(uploadCredit, head.Untransmitted);
                head.Untransmitted -= bytes;
                buffered -= bytes;
                uploadCredit -= bytes;
                if (head.Untransmitted > 0 || head.Unaccepted > 0) break;
                sendBuffer.Dequeue();
                goingUp.Enqueue((at + link.UploadLatency, head));
            }
            // An idle link can't save up bandwidth for later.
            if (sendBuffer.Count == 0) uploadCredit = Math.Min(uploadCredit, link.UploadRate * uploadShare * step);
        }
        while (!cut && goingUp.TryPeek(out var up) && up.At <= at)
        {
            goingUp.Dequeue();
            if (!uploadsDead(at)) RelayEnd.Arrive(up.Write);
        }
        while (!cut && goingDown.TryPeek(out var down) && down.At <= at)
        {
            goingDown.Dequeue();
            if (!downloadsDead(at)) ClientEnd.Arrive(down.Write);
        }
    }

    // The socket buffer takes what it has room for, in order; a write is done once all of it fits.
    private void Accept()
    {
        var done = new List<Write>();
        while (waiting.TryPeek(out var head))
        {
            var space = link.SendBufferBytes - buffered;
            if (space <= 0) break;
            var bytes = Math.Min(space, head.Unaccepted);
            if (!head.Buffered)
            {
                head.Buffered = true;
                sendBuffer.Enqueue(head);
            }
            head.Unaccepted -= bytes;
            head.Untransmitted += bytes;
            buffered += bytes;
            if (head.Unaccepted > 0) break;
            waiting.Dequeue();
            done.Add(head);
        }
        foreach (var write in done) write.Accepted.TrySetResult();
    }

    private Task Up(Write write)
    {
        if (cut) return Task.CompletedTask;
        waiting.Enqueue(write);
        Accept();
        return write.Accepted.Task;
    }

    private void Down(Write write)
    {
        if (cut) return;
        var at = now();
        downloadFreeAt = Math.Max(downloadFreeAt, at) + ((write.Bytes?.Length ?? 0) + Overhead) / link.DownloadRate;
        var arrival = Math.Max(lastArrival, downloadFreeAt + link.DownloadLatency + jitter.NextDouble() * link.DownloadJitter);
        if (downloadStalls.Within(arrival) is { } stallEnd) arrival = stallEnd;
        lastArrival = arrival;
        goingDown.Enqueue((arrival, write));
    }

    internal sealed class End(Wire wire, bool client, Action<object?> read, Func<object?> writing) : Stream
    {
        private readonly Queue<Write> readable = new();
        private TaskCompletionSource<int>? pendingRead;
        private Memory<byte> pendingBuffer;
        private CancellationTokenRegistration pendingCancel;
        private bool closed, ended;

        // What the write the last read took from was tagged with.
        public object? Tag { get; private set; }

        // The tag for the client's next writes, set around them.
        public object? Sending { get; set; }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Reads are asynchronous.");
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Writes are asynchronous.");

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (pendingRead != null) throw new InvalidOperationException("One read at a time.");
            if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled<int>(cancellationToken);
            if (TryRead(buffer) is { } count) return ValueTask.FromResult(count);
            var waitingRead = pendingRead = new TaskCompletionSource<int>();
            pendingBuffer = buffer;
            pendingCancel = cancellationToken.Register(() =>
            {
                if (pendingRead != waitingRead) return;
                pendingRead = null;
                waitingRead.TrySetCanceled(cancellationToken);
            });
            return new ValueTask<int>(waitingRead.Task);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (closed) throw new ObjectDisposedException(nameof(Wire));
            if (client) return new ValueTask(wire.Up(new Write(buffer.ToArray(), Sending)));
            wire.Down(new Write(buffer.ToArray(), writing()));
            return ValueTask.CompletedTask;
        }

        // What can be read now: from one write, as much of what's left of it as fits.
        private int? TryRead(Memory<byte> buffer)
        {
            if (closed) return 0;
            if (!readable.TryPeek(out var head)) return ended ? 0 : null;
            if (head.IsClose)
            {
                ended = true;
                return 0;
            }
            var count = Math.Min(buffer.Length, head.Bytes!.Length - head.Read);
            head.Bytes.AsSpan(head.Read, count).CopyTo(buffer.Span);
            head.Read += count;
            if (head.Read == head.Bytes.Length) readable.Dequeue();
            Tag = head.Tag;
            read(head.Tag);
            return count;
        }

        public void Arrive(Write write)
        {
            if (closed || ended) return;
            readable.Enqueue(write);
            if (pendingRead is not { } waitingRead || TryRead(pendingBuffer) is not { } count) return;
            pendingRead = null;
            pendingCancel.Dispose();
            waitingRead.TrySetResult(count);
        }

        // The link went down under this end: nothing more comes.
        public void Lost()
        {
            readable.Clear();
            ended = true;
            if (pendingRead is not { } waitingRead) return;
            pendingRead = null;
            pendingCancel.Dispose();
            waitingRead.TrySetResult(0);
        }

        protected override void Dispose(bool disposing)
        {
            if (closed) return;
            closed = true;
            var close = new Write(null, null);
            if (client) wire.Up(close);
            else wire.Down(close);
            if (pendingRead is { } waitingRead)
            {
                pendingRead = null;
                pendingCancel.Dispose();
                waitingRead.TrySetResult(0);
            }
            base.Dispose(disposing);
        }
    }
}
