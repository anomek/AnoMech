# AnoMech.Relay

A small WebSocket relay for AnoMech's multiplayer mode. It forwards frames between
clients in the same session code (`/session/<code>`), tagging each with its sender's
authenticated identity. It never decompresses or reads the messages it forwards; the
plugin checks every message it receives. Dalamud clients can't reach each other directly (NAT, and
AnoMech firewalls off FFXIV's own server traffic during a scenario), so this process
exists just to get them talking.

**There's no default/public relay bundled with the plugin.** Every group runs their
own — nothing connects until you type a URL into the plugin's Multiplayer screen. It's built to
be safe to run as a genuinely public service too (anyone, not just people you've
personally shared a URL with) — see [Running it as a public
service](#running-it-as-a-public-service) and [Security notes](#security-notes).

## Contents

- [Quick local test](#quick-local-test)
- [Option A — cloud VPS](#option-a--cloud-vps-recommended)
- [Option B — your own PC](#option-b--your-own-pc)
- [Adding TLS (`wss://`)](#adding-tls-wss)
- [Verifying it's reachable](#verifying-its-reachable)
- [What to share with your group](#what-to-share-with-your-group)
- [Running it as a public service](#running-it-as-a-public-service)
- [Logging](#logging)
- [Admin dashboard](#admin-dashboard)
- [Admin actions](#admin-actions)
- [Security notes](#security-notes)
- [Troubleshooting](#troubleshooting)
- [Configuring the plugin](#configuring-the-plugin)
- [Wire protocol](#wire-protocol)
- [Regression checks](#regression-checks)

---

## Quick local test

Same machine or LAN as your test partner? Skip the VPS:

```
cd AnoMech.Relay.Host
dotnet run -- --port 7890
```

- Host connects to `ws://127.0.0.1:7890`.
- Others on the LAN connect to `ws://<host's-LAN-IP>:7890` (`ipconfig` → IPv4 Address).
- Allow the app through Windows Firewall's private-network prompt if asked.

Prebuilt Windows, Linux, and macOS binaries are attached to each `relay-v*` GitHub
release. A Linux amd64/arm64 container is also published as
`ghcr.io/anomek/anomech-relay:<version>` and `ghcr.io/anomek/anomech-relay:latest`.
For example:

```bash
docker run --rm -p 7890:7890 ghcr.io/anomek/anomech-relay:latest
```

---

## Option A — cloud VPS (recommended)

Best when your group isn't all on one LAN. Any small Linux box works — a $4–6/mo VPS
is already overkill for a relay this light.

1. **Publish self-contained** (no .NET needed on the VPS):

   ```bash
   cd AnoMech.Relay.Host
   dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish
   ```

2. **Copy it over**:

   ```bash
   scp publish/AnoMech.Relay.Host youruser@your-vps-ip:/home/youruser/anomech-relay
   ```

3. **Run it once to confirm it starts**:

   ```bash
   ssh youruser@your-vps-ip
   chmod +x ~/anomech-relay
   ~/anomech-relay --port 7890
   ```

4. **Open the port**:

   ```bash
   sudo ufw allow 7890/tcp
   ```

   Providers with a network-level firewall (DigitalOcean, AWS, etc.) need the same
   port opened there too — `ufw` alone isn't enough.

5. **Run it as a systemd service** so it survives reboots. Create
   `/etc/systemd/system/anomech-relay.service`:

   ```ini
   [Unit]
   Description=AnoMech multiplayer relay
   After=network.target

   [Service]
   ExecStart=/home/youruser/anomech-relay --port 7890
   Restart=on-failure
   User=youruser

   [Install]
   WantedBy=multi-user.target
   ```

   Then:

   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable --now anomech-relay
   ```

6. **Point the plugin at it**: your VPS IP or domain (see [TLS](#adding-tls-wss) for
   `wss://`).

---

## Option B — your own PC

Free, but friends outside your LAN need a router port-forward, and your PC has to
stay on for the session.

1. **Publish self-contained** (`-r win-x64` on Windows, `-r linux-x64` on Linux):

   ```
   cd AnoMech.Relay.Host
   dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
   ```

2. **Open port 7890 in your firewall**, then **port-forward it on your router**
   (external TCP 7890 → this PC's LAN IP, port 7890) if anyone joining isn't on your
   home LAN.

3. **Find your public IP** (search "what is my ip") and share it as the relay
   address. No static IP? A dynamic-DNS service (No-IP, DuckDNS) gives you a stable
   hostname instead.

4. **Run it**: `.\publish\AnoMech.Relay.Host.exe --port 7890` (`./publish/AnoMech.Relay.Host
   --port 7890` on Linux). Closing the console kills it — see
   [Troubleshooting](#troubleshooting) if it won't start.

---

## Adding TLS (`wss://`)

Plain `ws://` is fine for testing, but some networks block unencrypted upgrades, and a
relay with `--token` or `--require-tls` refuses unencrypted connections. There are two ways
to serve `wss://`, and both need a domain name pointed at the relay's machine. A
certificate can't be issued for a bare IP, but a free dynamic-DNS name (DuckDNS, No-IP)
works.

- **[The relay serves it itself](#the-relay-serves-tls-itself)** with a Let's Encrypt
  certificate from certbot. It's one process with no proxy hop, so it costs the least CPU
  per player, and the relay keeps watch on the certificate for you.
- **[A reverse proxy in front](#a-reverse-proxy-in-front)** (Caddy, nginx) terminates
  TLS and passes plain `ws://` on to the relay.

A self-signed certificate won't work either way, because players' games refuse it.

### The relay serves TLS itself

These steps assume the VPS from [Option A](#option-a--cloud-vps-recommended), with
`relay.yourdomain.com` pointed at it.

1. **Get a certificate** with [certbot](https://certbot.eff.org/) (`sudo apt install
   certbot` on Debian/Ubuntu). In standalone mode, certbot answers Let's Encrypt's check
   on port 80 itself. Its timer renews the certificate the same way, unattended, so
   leave port 80 open:

   ```bash
   sudo ufw allow 80/tcp
   sudo ufw allow 443/tcp
   sudo certbot certonly --standalone -d relay.yourdomain.com
   ```

   If a web server already uses port 80, use certbot's plugin for it instead (`--nginx`,
   `--apache`, or `--webroot -w <its root>`). Any other ACME client (acme.sh, lego) also
   works, through `--tls-cert`/`--tls-key` below.

2. **Let the relay's user read the key.** certbot lets only root read it. Each renewed
   key gets the group and mode of the one before it, so you only do this once:

   ```bash
   sudo chgrp -R youruser /etc/letsencrypt/live /etc/letsencrypt/archive
   sudo chmod -R g+rX /etc/letsencrypt/live /etc/letsencrypt/archive
   ```

3. **Check it**, as the relay's user:

   ```bash
   ~/anomech-relay --tls-certbot relay.yourdomain.com --port 443 --check-tls
   ```

   The check shows the certificate, its dates, whether certbot renews it and which
   OpenSSL serves it. It then makes a TLS handshake with it and ends with `Ready: ...` or
   with what to fix. It doesn't start the relay.

4. **Run it on port 443.** In the systemd unit from [Option
   A](#option-a--cloud-vps-recommended), add the TLS flag, a reload command, and the
   right to use a port below 1024:

   ```ini
   [Service]
   ExecStart=/home/youruser/anomech-relay --port 443 --tls-certbot relay.yourdomain.com
   ExecReload=/bin/kill -HUP $MAINPID
   AmbientCapabilities=CAP_NET_BIND_SERVICE
   Restart=on-failure
   User=youruser
   ```

   ```bash
   sudo systemctl daemon-reload
   sudo systemctl restart anomech-relay
   journalctl -u anomech-relay -e
   ```

   The log's `TLS:` lines say what the relay serves. Players type just
   `relay.yourdomain.com`, because the plugin tries `wss://` on 443 first. Another port
   also works (`--port 7890`, with `relay.yourdomain.com:7890` in the plugin), but 443
   gets through networks that only let web traffic out.

| Flag | What it does |
|---|---|
| `--tls-certbot <domain>` | Serves `wss://` with certbot's certificate for `<domain>`, from `/etc/letsencrypt/live/<domain>/` |
| `--tls-cert <file>` `--tls-key <file>` | Serves `wss://` with these PEM files instead: the certificate with its chain (certbot's `fullchain.pem`, not `cert.pem`), and its private key |
| `--check-tls` | With either of the above: checks the certificate and a handshake with it, then exits |

**What the relay looks after by itself:**

- **Renewals.** The relay reads the certificate files again every minute. When they
  change, new connections get the new certificate, with no restart and no certbot deploy
  hook, and players already connected stay connected. `sudo systemctl reload
  anomech-relay` reads the files at once. If changed files don't load (a key that doesn't
  go with the certificate, a file the relay can't read), it logs why and keeps serving
  the certificate it had.
- **Expiry.** certbot renews a certificate with a third of its lifetime left. If one gets
  down to a quarter (about 22 days of a 90-day certificate) without being renewed, the
  relay warns at startup and then once a day, or every hour in the last tenth. The warning
  says how to find out why: `sudo certbot renew --dry-run`, and `systemctl list-timers |
  grep certbot` to check that certbot's timer runs.
- **OpenSSL updates.** The relay serves TLS with the system's OpenSSL, so OpenSSL's
  security updates arrive with the system's (`apt upgrade`, unattended-upgrades). But a
  running process keeps using the copy it loaded. When an update replaces that copy on
  disk, the relay warns once a day that it needs a restart for the update to apply.
  Restarting drops live sessions, so pick a quiet moment: `sudo systemctl restart
  anomech-relay`.
- **Wrong scheme.** A plain `http://` or `ws://` request at the TLS port gets a `400`
  saying to use `https://` or `wss://`. These and failed TLS handshakes are counted as
  `tls` in the [admin dashboard](#admin-dashboard)'s rejections.

**In Docker**, mount certbot's whole directory (the files in `live/` link into
`archive/`) and map 443 to the container's port. The image runs as group 1654, so give
the key to that group instead of the user from step 2:

```bash
sudo chgrp -R 1654 /etc/letsencrypt/live /etc/letsencrypt/archive
sudo chmod -R g+rX /etc/letsencrypt/live /etc/letsencrypt/archive
docker run -d --name anomech-relay --restart unless-stopped -p 443:7890 \
  -v /etc/letsencrypt:/etc/letsencrypt:ro \
  ghcr.io/anomek/anomech-relay:latest --tls-certbot relay.yourdomain.com
```

The container picks up renewals the same way, and `docker kill --signal=HUP
anomech-relay` makes it read them at once. It has its own OpenSSL, so OpenSSL's updates
come with a newer image: run `docker pull ghcr.io/anomek/anomech-relay:latest`, then
recreate the container.

### A reverse proxy in front

**[Caddy](https://caddyserver.com/)** is the easiest: automatic Let's Encrypt certs,
almost no config. With a domain pointed at your VPS:

```
relay.yourdomain.com {
    reverse_proxy localhost:7890
}
```

Run `caddy run` (or as a systemd service) and point the plugin at
`wss://relay.yourdomain.com`. No port is needed, because Caddy handles 443 → 7890. With
a token or `--require-tls`, the relay also needs `--trusted-proxy` (see [Running it as a
public service](#running-it-as-a-public-service)).

---

## Verifying it's reachable

From another machine (or your phone's data connection):

```powershell
Test-NetConnection -ComputerName your-vps-ip -Port 7890
```

`TcpTestSucceeded : True` means it's open. `False` means the relay isn't running, or
a firewall/port-forward isn't set up — see [Troubleshooting](#troubleshooting).

For a real WebSocket-level check, use
[`websocat`](https://github.com/vi/websocat):

```
websocat ws://your-vps-ip:7890/session/TEST
```

Connects and hangs waiting for input → working.

If the relay serves TLS itself, `curl https://relay.yourdomain.com/info` from anywhere
should print its JSON. `curl` checks the certificate the same way players' games do.

---

## What to share with your group

- **The relay URL** — same for everyone, doesn't change between sessions.
- **The session code** — assigned by the relay when the host clicks "Host new
  session" (guaranteed not already in use), shown in-window with a Copy button. A
  code with no traffic for 10 seconds is disbanded automatically.

---

## Running it as a public service

Everything in [Quick local test](#quick-local-test)/[Option A](#option-a--cloud-vps-recommended)
still applies — these just add the flags worth setting once real strangers (not just
your own group) can reach the port.

```
anomech-relay --port 7890 --token <shared-secret> --admin-token <a-different-secret>
```

- **`--token <value>`** — an access password. Once set, `/host` and `/session/<code>`
  both require it (sent as a header, never in the URL/query string). Hand it out to
  the people you actually want using this relay; everyone else gets `401` before a
  WebSocket ever opens. Leave unset to keep the original "anyone with the URL" model.
  The plugin's Multiplayer screen only shows a password field when the relay it's
  pointed at actually has one set (see [Configuring the plugin](#configuring-the-plugin)).

  **Setting `--token` also enforces TLS.** A password sent in the clear isn't a
  password. Once `--token` (or `--admin-token`) is set, the relay refuses (`426`) any
  connection it can't confirm was encrypted. A connection over the relay's [own
  TLS](#the-relay-serves-tls-itself) always is. One through a [reverse
  proxy](#a-reverse-proxy-in-front) counts when the proxy says so with
  `X-Forwarded-Proto: https`, which Caddy and nginx both set automatically. A direct
  `ws://` connection is rejected the same way, including your own [quick local
  test](#quick-local-test), so serve TLS before setting a token, or don't set one for
  pure local testing. The plugin independently refuses to send a saved password over
  anything but `wss://` too, so this is enforced on both ends, not just trusted to the
  relay operator having configured TLS correctly.

  **Minimum length: 16 characters, enforced at startup.** The relay refuses to start
  (not just warn) if `--token`/`--admin-token` is shorter — a short shared secret is
  still guessable over time even with the lockout in place. Generate one with e.g.
  `openssl rand -hex 16`.

  **Can also come from an environment variable** (`ANOMECH_RELAY_TOKEN`) instead of
  the CLI flag — the flag wins if both are set. A CLI argument is visible to any other
  local user via a process listing (`ps`/Task Manager) and often ends up preserved in
  shell history; an env var avoids both of those, if you'd rather not type the secret
  directly on the command line.
- **`--admin-token <value>`** — a *separate* secret gating the [admin
  dashboard](#admin-dashboard). Keep it different from `--token`: people you hand the
  join password to don't necessarily need to see live abuse counters and connection
  state. Leave unset and the admin endpoint doesn't exist at all (404, not just 401).
  Also subject to the same TLS enforcement, minimum length, and env var
  (`ANOMECH_RELAY_ADMIN_TOKEN`) as `--token` above.
- **`--require-tls`** — enforces the same TLS check as above (the relay's own TLS, or
  `X-Forwarded-Proto: https` from a trusted proxy; otherwise `426`) even with no
  `--token`/`--admin-token` set at all. A relay with no password still carries session
  codes and full match state; this is for an operator who wants everything encrypted
  end-to-end regardless of whether a secret is involved. Applies to `/info` too, which
  otherwise carries nothing sensitive and wouldn't need it on its own — the point is no
  exceptions, not case-by-case.
- **Tuning flags**, all optional (defaults are sane for moderate public traffic; raise
  or lower to match your actual load):
  | Flag | Default | What it caps |
  |---|---|---|
  | `--max-sessions` | 500 | Live rooms process-wide |
  | `--max-peers-per-session` | 8 | Peers in one room |
  | `--max-connections-per-ip` | 64 | Live sockets from one source address at once, across every room. Sized with slack for CGNAT/mobile carriers sharing one IP across many real users — a public relay sees much more of this than a friend-only one, so don't set it too tight (see [Security notes](#security-notes)) |
  | `--max-message-bytes` | 1048576 (1 MiB) | One logical message's size |
  | `--max-messages-per-second` | 5000 | Messages from one connection before it gets cut off. A host broadcasting a snapshot pair every frame peaks near 450/s, so this is ~10x the worst legitimate case |
  | `--max-bytes-per-second` | 10485760 (10 MiB) | Bytes from one connection per second. A full 8-peer run peaks around 2 MB/s on the host's connection |
  | `--max-fragments-per-message` | 2000 | Fragments allowed while assembling one message, independent of its byte size — bounds someone deliberately sending many tiny frames to burn CPU rather than a large one |
  | `--max-failed-joins` | 10 | Failed attempts per address before a 5-minute lockout — shared across session-code guesses and a wrong `--token`. Wrong `--admin-token` attempts use their own separate bucket, so an admin-endpoint scan can never lock players out |
  | `--usage-warn-fraction` | 0.5 | Logs one `[NEAR-LIMIT]` line per connection once it passes this fraction of either rate cap — how you find out a real scenario is creeping toward a limit before anyone is cut off |
  | `--bind` | `*` (all interfaces, IPv4 and IPv6) | Address to listen on: an IP, `*`, or `localhost`. Set `127.0.0.1` when a reverse proxy fronts the relay, so nothing can reach it directly |
  | `--log-dir` | `logs/` next to the executable | Where compressed logs are written — see [Logging](#logging) |
  | `--log-max-bytes` | 5368709120 (5 GiB) | Total on-disk size of all log segments combined |

- **`--trusted-proxy <cidr>`** — **required when a token or `--require-tls` is set and
  a reverse proxy terminates TLS** (not when the relay serves TLS itself), and
  repeatable. Names the reverse proxy in front of the relay (e.g. `--trusted-proxy
  127.0.0.1/32` for a local Caddy). Two things depend on it:

  - **Client addresses.** Behind a proxy, every connection's transport address is the
    proxy's, so without this every per-IP control — the connection cap, the brute-force
    lockout, the abuse log — would collapse into one shared bucket. One person guessing
    session codes would lock out *everyone*. With it, the relay reads the real client
    address from `X-Forwarded-For` (override the header name with `--client-ip-header`,
    e.g. `CF-Connecting-IP`).
  - **TLS enforcement.** `X-Forwarded-Proto: https` is only believed from one of these
    addresses. Otherwise anyone who can reach the port directly could just claim it.

  Never list an address that isn't actually your proxy — a host in this list is trusted
  to say who its traffic is coming from. The relay **refuses to start** if TLS is
  enforced and it has neither its own TLS nor this, rather than silently doing the wrong
  thing. Requests arriving over loopback with no forwarding headers at all are exempt
  from the TLS check (they never left the machine) — this is what lets the admin CLI
  reach a local relay.

**Weigh a reverse proxy in front.** The relay's own HTTP handling is a minimal
hand-rolled front door with far less adversarial-traffic hardening than nginx or Caddy,
which have absorbed years of internet-facing attack traffic. Serving TLS from the relay
itself saves the proxy's CPU and memory, so the same box carries more players. A proxy
in front is the more cautious choice for a relay open to everyone. [Adding
TLS](#adding-tls-wss) covers both.

**Give the process real OS-level resource ceilings.** The systemd unit in [Option
A](#option-a--cloud-vps-recommended) has none by default. Add to the `[Service]`
block:

```ini
MemoryMax=512M
LimitNOFILE=65536
CPUQuota=80%
```

Sizes above are a starting point, not a recommendation — tune to your box. `LimitNOFILE`
matters most: each live connection holds a file descriptor, and the OS default (often
1024) caps you well below `--max-sessions × --max-peers-per-session` long before the
app-level limits do anything.

**A single process cannot stop a real distributed attack.** Everything above defends
against a single bad actor or a single-source flood. A botnet spread across thousands
of IPs sails past any per-IP cap while still exhausting the total-session limit in
aggregate — that needs infrastructure-level DDoS protection (a host/CDN that provides
it, e.g. Cloudflare in front). No amount of code in this process substitutes for that;
see [Security notes](#security-notes) for the full reasoning.

---

## Logging

On by default — everything the console prints (session lifecycle, rejections, alerts,
summaries) also lands in a compressed log directory, plus much finer detail that would
otherwise drown out the console (every individual rejected attempt, and every message
broadcast — size, type, sender, how many peers it reached). Message *contents* are
never logged, only that metadata; see [Security notes](#security-notes).

Logs live in `logs/` next to the executable by default (`--log-dir` to change it,
`--no-file-log` to disable file logging entirely and keep console-only output). Each
segment is plain text while active and gets gzip-compressed once it hits 64 MiB; the
oldest compressed segments are deleted as needed to keep the directory's total size
under `--log-max-bytes` (5 GiB by default) — the currently-active segment is never
deleted. A `journalctl`/log-rotation setup on top of this is optional, not required.

**Reading back one session's logs:**

```
anomech-relay --session-log ABCD23 --log-dir logs
```

Scans every segment — live and compressed — for lines tagged with that session code
and prints them in order. Useful for reconstructing what happened around a specific
report (a disconnect, a suspected abuse attempt, a bug) without needing to `zgrep`
through `.gz` files by hand.

---

## Admin dashboard

A live text dashboard for whoever's running the relay — session/connection counts,
per-category rejection totals, memory/GC stats. Requires `--admin-token` to be set on
the relay (see above).

```
anomech-relay --admin --host https://relay.yourdomain.com --admin-token <the-same-value>
```

`--host` defaults to `http://localhost:<port>` if omitted, for running it on the same
box as the relay. A relay that serves TLS itself needs `--host
https://relay.yourdomain.com` even there, because its certificate names the domain, not
localhost. Refreshes every 2 seconds; Ctrl+C to exit. Nothing here requires running the
dashboard continuously — it's a `curl`-style snapshot tool you open when you want to
look, not a required always-on process. The same data is available as
plain JSON at `GET /admin/stats` (with the `X-AnoMech-Admin-Token` header) if you want
to pull it into something else.

The relay also logs a one-line summary every minute on its own (session/peer/IP
counts) and an `[ALERT]` line if rejected connections spike within a ~2s window —
both land in the same console/journal output as everything else, no dashboard needed
to notice something's wrong.

---

## Admin actions

The dashboard (`--admin`) is interactive. Single keypresses:

| Key | Action |
|---|---|
| `s` | Toggle the live session list — every room, its peers, their addresses, and per-connection traffic/peak rates |
| `k` | Kick one connection by id (ids are shown in the session list) |
| `d` | Disband a whole session |
| `b` / `u` | Ban / unban an address. A ban also drops that address's live connections immediately |
| `c` | Clear every lockout and failure counter |
| `p` / `r` | Pause / resume accepting new connections. Existing sessions keep running |
| `l` | Change a limit live, without restarting (any of the `--max-*` flags, plus `usage-warn-fraction`) |
| `q` | Quit the dashboard (the relay keeps running) |

The same actions are available as `POST /admin/action` with a JSON body
(`{"action":"kick-peer","connectionId":12}`), alongside `GET /admin/stats` and
`GET /admin/sessions`. All three need the `X-AnoMech-Admin-Token` header.

Bans and limit changes live in memory only — they reset when the relay restarts.

---

## Security notes

- **Access control**: no auth by default — anyone with the URL and a live session code
  can join (capped at 8 peers per session). Set `--token` to require a shared password
  for anyone to connect at all; see [Running it as a public
  service](#running-it-as-a-public-service), including the TLS enforcement that comes
  with it. Trust is scoped to one session, not the whole relay: anyone who has that
  session's code can join it, and nothing about one session carries over to another.
  The host's kick and ban are carried out by the relay itself, which closes the
  connection. A ban also covers the player's address (IPv6 by /64) for the rest of the
  session, including other connections already using it, so players sharing a network
  are removed together; the host is told who they were. Banning a player who happens to
  be disconnected still keeps them out.
- **Session-code guessing**: codes are drawn from a cryptographically random
  generator (not a predictable PRNG), so a stranger who's observed some issued codes
  can't predict a future one. Repeated failed joins from one address get locked out
  for 5 minutes (`--max-failed-joins`).
- **Resource exhaustion (the relay itself)**: caps on total sessions, peers per
  session, connections per address, one message's size, and both the message rate and
  the byte rate of a single connection, plus timeouts on a stalled handshake, a message
  that never finishes arriving, and every close handshake. Each rate cap is roughly 10x
  what a full 8-peer run produces, and the relay logs a `[NEAR-LIMIT]` line plus a
  peak-vs-cap figure in its minute summary so you can see headroom rather than guess at
  it. All tunable via CLI flags (and live from the admin dashboard); see [Running it as
  a public service](#running-it-as-a-public-service).
- **IPv6 address rotation**: per-address controls bucket IPv6 by /64, not by single
  address — a /64 is the standard allocation, so keying on the full address would make
  every cap free to sidestep.
- **CGNAT / shared-IP collateral**: at public scale, unrelated strangers legitimately
  share one address more often than in a friend-only deployment (mobile carriers,
  corporate NAT). `--max-connections-per-ip` defaults with slack for this, but if you
  see real users getting capped, raise it rather than assume it's abuse.
- **Message impersonation**: each plugin install keeps a private random secret and gives
  every relay address its own credential derived from it, so one relay can't replay what
  it saw on another. The relay derives the player's public id from that credential and
  tags every forwarded message with that id, the connection number, and whether it's the
  room's host, so neither the id nor host status can be claimed by anyone else. A peer therefore can't forge a host message (world state, run
  start/end, ...) or send one under somebody else's id to steal their role; the plugin
  drops both. A host that reconnects is recognized by the same credential and is the
  host again. Peer messages go only to the host.
- **Bounded parsing (in the plugin)**: the relay forwards message bodies untouched. The
  receiving plugin caps decompression (8 MiB from the host, 64 KiB from a peer), checks
  that a peer's message is a peer type naming its real sender before reading it, and
  holds each peer to its own rate. A message that fails is dropped on its own; the connection stays up.
- **Redirects**: the plugin never follows a redirect when connecting, so a relay can't
  pass the credential or the relay password on to another server.
- **It cannot be used to attack a third party.** It's TCP (WebSocket), not the
  connectionless UDP protocols IP-spoofing reflection/amplification attacks need — you
  can't fake a TCP source address without completing the handshake back to that faked
  address, which the real attacker can't do. It's also not an open proxy: it never
  opens an outbound connection anywhere a client tells it to, only ever forwarding
  between sockets that each independently connected in. The one amplification that
  does exist (one message fans out to up to `--max-peers-per-session - 1` others) can
  only ever land on people already in that same session, not an arbitrary target, and
  is bounded by the message-size cap.
- **Volumetric/distributed attacks are out of scope for this process.** Everything
  above stops a single bad actor. A real botnet needs infrastructure-level DDoS
  protection in front (see [Running it as a public
  service](#running-it-as-a-public-service)) — no in-process code can substitute for
  that.
- **Logging**: connection open/close, peer counts, connecting IPs, every rejection
  reason, and every message's shape (size/type/sender/how many peers it reached) are
  logged — compressed and capped on disk by default; see [Logging](#logging). Message
  *contents* are never logged, only that metadata. A `--admin-token`-gated
  `/admin/stats` endpoint and the [admin dashboard](#admin-dashboard) expose live
  counters without needing to read logs at all.
- Running it on a shared machine opens one more port — don't reuse a port something
  else is already listening on, and don't leave it forwarded on your router longer
  than you're actually using it.

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| "Disconnected" immediately after Host/Join | Relay isn't running, or the URL/port is wrong. Check the relay's own console/journal output. |
| `Test-NetConnection` fails from outside | VPS firewall/security group isn't open, or the router port-forward doesn't match the PC's current LAN IP (consider a static DHCP lease). |
| Works locally, not for others | Testing with a LAN IP but gave others your public IP without port-forwarding, or vice versa. |
| `Failed to bind` on startup | Another process owns the port — check `netstat -ano \| findstr 7890` (Windows) or `ss -ltnp` (Linux). |
| `Failed to bind *:443: Permission denied` | Ports below 1024 need `AmbientCapabilities=CAP_NET_BIND_SERVICE` in the service (see [serving TLS itself](#the-relay-serves-tls-itself)). |
| `Failed to bind *:443: Address already in use` | A reverse proxy from an earlier setup still holds the port: `sudo systemctl disable --now caddy` (or nginx). |
| The relay won't start, with a `TLS:` line saying it isn't allowed to read a file | Step 2 of [serving TLS itself](#the-relay-serves-tls-itself): let the relay's user read certbot's files. The line gives the exact commands. |
| The plugin falls back to `ws://`, or says the certificate isn't valid | Players can't verify the certificate the relay serves: it's self-signed, it's for another name, or it lacks its chain (`--tls-cert` needs `fullchain.pem`, not `cert.pem`). `--check-tls` shows the names it serves and flags the other two. |
| The log warns the certificate `hasn't been renewed` | certbot's renewal is failing. `sudo certbot renew --dry-run` shows why, which is often port 80 being closed. |
| The log says `OpenSSL was updated on this machine` | Restart the relay at a quiet moment so the update applies: `sudo systemctl restart anomech-relay`. |
| "session full" | 8 peers already connected; host a new session. |
| Connects, nothing happens after Join | Confirm the same relay URL and session code on both ends (case-normalized, but typos happen). |

## Configuring the plugin

Open the Multiplayer screen (`/anomech mp`, or the **Multiplayer** button in the main
window while in an inn) and type your relay's address into the **Server** field. Just the address is enough (`relay.example.com`, or
`203.0.113.5:7890` without TLS) — the plugin tries `wss://` first and falls back to
`ws://` if that relay doesn't support it or hasn't answered within 5 seconds, telling
you which one it used. An explicit `ws://`/`wss://` also works. Remembered across
sessions once set.

If the relay you typed requires `--token`, a **Password** field appears
automatically underneath (the plugin asks the relay's plain `/info` endpoint whether
one is needed before showing it) — also remembered across sessions. A relay with no
token set never shows the field at all. The saved password is tied to the relay it was
entered for and is never sent to any other address, nor over `ws://`.

## Wire protocol

Update the relay and the plugin together; older builds of either are refused.
Connections send `X-AnoMech-Protocol: 1` and a 64-character hexadecimal
`X-AnoMech-Peer-Secret`. Treat the latter as a credential and keep it out of proxy
logs. The public peer id is the first 16 bytes of its SHA-256 digest, read as a .NET
GUID. The relay's greeting is JSON with `relayVersion`, `capabilities`
(`binaryCompression`, `authenticatedIdentity`, `roomModeration`), the `peerId` it
derived, and, for `/host`, the `sessionCode`.

Clients send JSON as text frames, or Brotli-compressed JSON as binary frames. Every
frame the relay forwards is binary: host flag (1 byte), connection id (4 bytes, little
endian), sender id (16 bytes, .NET GUID layout), compression flag (1 byte), then the
body exactly as it was sent. A frame with connection id 0 and an empty sender is a
notice from the relay itself.

The host kicks, bans and unbans with an uncompressed text frame of at most 1 KiB whose
first property is `t`: `{ "t": "relayControl", "Operation": "kick|ban|unban", "PeerId":
"..." }`. The relay acts on it instead of forwarding it, and ignores one from anyone but
the host. When a ban removes other connections on the same address, the relay tells the
host with `{ "t": "relayNotice", "Removed": [ ... ] }`.

## Tests

From the repository root, with a .NET 10 runtime:

```
dotnet test tests/AnoMech.Relay.Tests
```

These cover the wire format, the relay's HTTP front door, and its own TLS. The plugin-side suite
(`dotnet test tests/AnoMech.Tests`, needs Dalamud installed) also runs the real relay
against the plugin's client over loopback WebSockets: rooms, routing, moderation.
