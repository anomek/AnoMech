"""Extract a single pull from an FFXIV ACT/IINACT network log into its own file.

Reuses parser.find_pulls to locate the combat window, then copies every raw
line whose timestamp falls within [pull.start, pull.end] into the output file.
The most recent 01|ChangeZone line before the window is prepended so the slice
is a self-contained, parseable network log: running

    python parser.py <out_file> <territory>

re-detects it as pull 1 with the same relative times as the source pull.

Usage:
    python extract_pull.py <log_path> <pull_index> [-t <territory_decimal>]
                           [-o <out_path>] [--pad <seconds>]

Example:
    python extract_pull.py ..\\logs\\UMAD\\Split-Network_...log 5 -t 1363
"""

from __future__ import annotations

import argparse
import os
import sys
from datetime import timedelta

from parser import find_pulls, iter_records, parse_timestamp


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("log", help="Path to the source network log")
    ap.add_argument("pull", type=int, help="1-based pull index (see parser.py)")
    ap.add_argument(
        "-t", "--territory", type=int, default=None,
        help="Territory id (decimal) to scope pull numbering to. "
             "Omit to number pulls across every territory in the log.",
    )
    ap.add_argument(
        "-o", "--output", default=None,
        help="Output path. Defaults to '<log_stem>_pullNN.log' next to the source.",
    )
    ap.add_argument(
        "--pad", type=float, default=0.0,
        help="Seconds of lead-in/lead-out to include around the combat window "
             "(default 0 = exact window). Lead-in never crosses into the prior pull.",
    )
    args = ap.parse_args(argv)

    pulls = find_pulls(args.log, args.territory)
    if not pulls:
        print("No pulls found.", file=sys.stderr)
        return 1
    if not (1 <= args.pull <= len(pulls)):
        print(f"Pull {args.pull} out of range (1-{len(pulls)}).", file=sys.stderr)
        return 1

    pull = pulls[args.pull - 1]
    if pull.end is None:
        print("Selected pull has no detected end (still open); aborting.", file=sys.stderr)
        return 1

    start = pull.start - timedelta(seconds=args.pad)
    # Don't let the lead-in swallow the tail of the previous pull.
    if args.pull >= 2 and pulls[args.pull - 2].end is not None:
        prev_end = pulls[args.pull - 2].end
        if start < prev_end:
            start = prev_end
    end = pull.end + timedelta(seconds=args.pad)

    out_path = args.output
    if out_path is None:
        stem, _ = os.path.splitext(args.log)
        out_path = f"{stem}_pull{args.pull:02d}.log"

    last_zone_line: str | None = None
    written = 0
    # Source lines are chronological; allow a small grace past `end` for any
    # late-arriving out-of-order row, then stop.
    grace = timedelta(seconds=5)

    with open(out_path, "w", encoding="utf-8", newline="\n") as out:
        for parts in iter_records(args.log):
            if len(parts) < 2:
                continue
            try:
                ts = parse_timestamp(parts[1])
            except ValueError:
                continue
            raw = "|".join(parts)
            if ts < start:
                if parts[0] == "01":
                    last_zone_line = raw  # remember zone context for the slice
                continue
            if ts > end:
                if ts > end + grace:
                    break
                continue
            if last_zone_line is not None:
                out.write(last_zone_line + "\n")
                last_zone_line = None
            out.write(raw + "\n")
            written += 1

    print(
        f"Pull {args.pull} ({pull.zone_name}, {pull.outcome}): "
        f"{pull.start} -> {pull.end} ({pull.duration_s:.0f}s)",
        file=sys.stderr,
    )
    print(f"Wrote {written} lines to {out_path}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))