using System;
using System.IO;
using NAudio.Wave;
using NVorbis;

namespace AnoMech.Core.Game;

// Volume scales the samples: the output device's own volume would change the game's.
internal sealed class MusicPlayer : IDisposable
{
    private readonly VorbisReader reader;
    private readonly WaveOutEvent output;
    // A track without LoopStart/LoopEnd tags plays once, as its SCD loop is empty too.
    private readonly bool loops;
    private readonly long loopStart;
    private readonly long loopEnd;
    private const float FadeInSeconds = 1f;
    private readonly long fadeInFrames;
    private long framesPlayed;
    private long fadeOutStart = -1;
    private long fadeOutFrames;
    private readonly long latencyFrames;
    private float[] samples = [];
    private readonly object gate = new();
    private volatile float volume;
    private bool disposed;

    public float Volume
    {
        get => volume;
        set => volume = Math.Clamp(value, 0f, 1f);
    }

    // True once the fade's last frame has left the output buffer.
    public bool FadedOut
    {
        get { lock (gate) return disposed || (fadeOutStart >= 0 && framesPlayed >= fadeOutStart + fadeOutFrames + latencyFrames); }
    }

    public double PositionSeconds
    {
        get { lock (gate) return disposed ? 0d : (double)reader.SamplePosition / reader.SampleRate; }
    }

    private MusicPlayer(VorbisReader reader, float initialVolume)
    {
        this.reader = reader;
        var hasStart = long.TryParse(reader.Tags.GetTagSingle("LoopStart", false), out var start);
        var hasEnd = long.TryParse(reader.Tags.GetTagSingle("LoopEnd", false), out var end);
        loops = hasStart && hasEnd && start >= 0 && end > start;
        loopStart = loops ? start : 0;
        loopEnd = loops ? Math.Min(end, reader.TotalSamples) : reader.TotalSamples;
        fadeInFrames = (long)(FadeInSeconds * reader.SampleRate);
        Volume = initialVolume;
        output = new WaveOutEvent { DesiredLatency = 200 };
        latencyFrames = (long)output.DesiredLatency * reader.SampleRate / 1000;
        output.Init(new RawSourceWaveStream(new PcmStream(this), new WaveFormat(reader.SampleRate, 16, reader.Channels)));
    }

    public static MusicPlayer? Start(byte[] ogg, double seconds, float initialVolume, out string error)
    {
        error = "";
        MusicPlayer? player = null;
        try
        {
            var reader = new VorbisReader(new MemoryStream(ogg, writable: false), true);
            try { player = new MusicPlayer(reader, initialVolume); }
            catch { reader.Dispose(); throw; }
            var target = Math.Max(0L, (long)(seconds * reader.SampleRate));
            if (player.loops && target >= player.loopEnd)
                target = player.loopStart + (target - player.loopStart) % (player.loopEnd - player.loopStart);
            if (target >= reader.TotalSamples)
            {
                error = $"{seconds:F2}s is past the end of a track that doesn't loop ({reader.TotalSamples / (double)reader.SampleRate:F2}s)";
                player.Dispose();
                return null;
            }
            reader.SamplePosition = target;
            player.output.Play();
            return player;
        }
        catch (Exception e)
        {
            error = $"{e.GetType().Name}: {e.Message}";
            player?.Dispose();
            return null;
        }
    }

    public void FadeOut(float seconds)
    {
        lock (gate)
        {
            if (disposed || fadeOutStart >= 0) return;
            fadeOutStart = framesPlayed;
            fadeOutFrames = Math.Max(1L, (long)(seconds * reader.SampleRate));
        }
    }

    private int ReadPcm(byte[] buffer, int offset, int count)
    {
        var channels = reader.Channels;
        var sampleCount = count / (2 * channels) * channels;
        if (samples.Length < sampleCount) samples = new float[sampleCount];
        Fill(samples, sampleCount);
        for (var i = 0; i < sampleCount; i++)
        {
            var value = (short)Math.Clamp(samples[i] * 32767f, -32768f, 32767f);
            buffer[offset + 2 * i] = (byte)value;
            buffer[offset + 2 * i + 1] = (byte)(value >> 8);
        }
        return sampleCount * 2;
    }

    private void Fill(float[] buffer, int count)
    {
        lock (gate)
        {
            if (disposed)
            {
                Array.Clear(buffer, 0, count);
                return;
            }
            var channels = reader.Channels;
            var written = 0;
            var emptyReads = 0;
            while (written < count)
            {
                var framesLeft = loopEnd - reader.SamplePosition;
                var wanted = (int)Math.Min(count - written, Math.Max(framesLeft, 0) * channels);
                var got = wanted > 0 ? reader.ReadSamples(buffer, written, wanted) : 0;
                written += got;
                if (got > 0 && reader.SamplePosition < loopEnd) { emptyReads = 0; continue; }
                // A stream that yields nothing at its own loop start would spin here.
                if (!loops || (got == 0 && ++emptyReads > 1))
                {
                    Array.Clear(buffer, written, count - written);
                    break;
                }
                reader.SamplePosition = loopStart;
            }
            var v = volume;
            for (var i = 0; i < count; i++)
            {
                var frame = framesPlayed + i / channels;
                var gain = frame < fadeInFrames ? v * frame / fadeInFrames : v;
                if (fadeOutStart >= 0) gain *= Math.Clamp(1f - (float)(frame - fadeOutStart) / fadeOutFrames, 0f, 1f);
                buffer[i] *= gain;
            }
            framesPlayed += count / channels;
        }
    }

    public void Dispose()
    {
        output.Stop();
        output.Dispose();
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            reader.Dispose();
        }
    }

    private sealed class PcmStream(MusicPlayer player) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => long.MaxValue;
        public override long Position { get => 0; set { } }
        public override int Read(byte[] buffer, int offset, int count) => player.ReadPcm(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
