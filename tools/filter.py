"""Packet-level drop filter for ACT/IINACT network log rows.

Every "should we drop this packet entirely?" decision lives here so the
filtering policy can be reviewed in one place. `should_drop(opcode, parts,
ctx)` is the single entry point — callers in parser.py call it before
invoking a formatter, and the formatters assume any row they receive has
already passed the filter.

What stays in parser.py:
  - Per-key suppression inside a kept 261|Change packet (HP/MP/Heading/...
    are silently dropped from the rendered output, recorded here via
    `record_dropped_change_key`).
  - Code-gen merge passes (263 -> 20, 21/22 -> 20).
  - Instance-lifetime windowing (`pick` / `pick_for_add`).
These are not yes/no decisions on a single isolated packet, so they need
adjacent-row or per-field context the formatter already has."""

from __future__ import annotations
from dataclasses import dataclass


@dataclass(frozen=True)
class FilterContext:
    """View from which we're inspecting the row.
      - Per-NPC view: caller sets `npc_id` to the actor whose timeline is
        being populated, and `pet_ids` to the set of player-pet actor ids
        detected upstream (any 03 with a player-owned owner field).
      - Instance / director view: caller leaves `npc_id` None and `pet_ids`
        empty — these filters only care about the source actor of the row.
    """
    npc_id: str | None = None
    pet_ids: frozenset[str] = frozenset()


# Opcodes that produce no useful information for any view: DoT/HoT ticks,
# job-init / cancel-cast lookalikes, status heartbeats, and the state-sync
# pings whose payloads are already covered by other opcodes.
ALWAYS_DROP_OPCODES: frozenset[str] = frozenset({
    "24",  # NetworkDoT/HoT tick — fires every few seconds per active dot.
    "31",  # In this log a job-change / init event, not cancel-cast.
    "37",  # NetworkActionSync hp-update — redundant with 21/22 hp suffix.
    "38",  # State sync — periodic, no new info.
    "39",  # Periodic actor hp/mp/pos ping — redundant with 270/271.
})


# Routine state-sync keys silently dropped from 261|Change packets even when
# the packet itself survives (because some OTHER key is interesting). Per-key
# suppression happens in parser.py's formatter; this list is the source of
# truth referenced by both modules.
CHANGE_KEY_DROPS: frozenset[str] = frozenset({
    "MaxHP", "CurrentHP", "Heading", "CastBuff",
    "NPCTargetID", "PCTargetID", "CurrentMP",
})


# Position keys aren't dropped per-key — they fold into a single `pos=...`
# field — but they also don't make a 261|Change packet "interesting" on
# their own. A Change carrying only pos survives only if some other key
# (ModelStatus, an unrecognised field) tagged it as interesting.
_POS_KEYS: frozenset[str] = frozenset({"PosX", "PosY", "PosZ"})


# Distinct keys filtered out across this run — populated by `should_drop`
# (whole-packet drops) and by parser.py's 261|Change formatter (per-key
# drops). Surfaced at the end of an extended dump and as a header comment
# in --code output.
_DROPPED_CHANGE_KEYS: set[str] = set()


def get_dropped_change_keys() -> list[str]:
    return sorted(_DROPPED_CHANGE_KEYS)


def record_dropped_change_key(key: str) -> None:
    _DROPPED_CHANGE_KEYS.add(key)


def _safe(parts: list[str], idx: int) -> str:
    return parts[idx] if 0 <= idx < len(parts) else ""


def _looks_player(actor_id: str) -> bool:
    return bool(actor_id) and actor_id[0] == "1"


def _change_packet_interesting(parts: list[str]) -> tuple[bool, list[str]]:
    """Walk a 261|Change body and decide whether anything in it would
    render. Returns (interesting, seen_keys). A packet is interesting iff
    it carries ModelStatus or an unrecognised key — every drop-list key and
    every position key contributes nothing on its own."""
    pairs = parts[4:-1]
    if len(pairs) % 2 != 0:
        # Truncated / malformed — keep, let the formatter surface raw tail.
        return True, []
    seen: list[str] = []
    interesting = False
    for i in range(0, len(pairs), 2):
        key = pairs[i]
        seen.append(key)
        if key in CHANGE_KEY_DROPS or key in _POS_KEYS:
            continue
        interesting = True  # ModelStatus and unknown keys both count.
    return interesting, seen


def should_drop(opcode: str, parts: list[str], ctx: FilterContext) -> bool:
    """Return True when this row should be dropped from the caller's view.

    Per-NPC view filters (active when `ctx.npc_id` is set):
      - 20/21/22 (cast/ability), 26 (status apply), 30 (status remove):
        drop player- or pet-sourced rows targeting this NPC; the player-
        side log already covers them.
      - 35 (tether): drop tethers sourced by a player pet (Esteem/Bahamut
        latching the boss).

    View-independent filter for 30 (NetworkBuffRemove): drop rows whose
    duration field isn't "0.00" (the canonical removal marker — every type-30
    in ACT logs is 0.00, but the guard keeps malformed lines out).

    Instance / director view filters (active when `ctx.npc_id` is None):
      - 33 (ActorControl): drop NPC- or player-sourced events — those
        belong in the per-NPC dump. Only director-sourced rows (source id
        starts with `8`) survive.

    View-independent filters:
      - `ALWAYS_DROP_OPCODES` set (24/27/30/31/37/38/39).
      - 261|Change: drop when no key is interesting (only state-sync
        churn). Records every key seen so callers can summarise what was
        suppressed wholesale. 261|Add and 261|Remove always pass."""

    if opcode in ALWAYS_DROP_OPCODES:
        return True

    if opcode == "30" and _safe(parts, 4) != "0.00":
        return True

    if ctx.npc_id is not None:
        if opcode in ("20", "21", "22"):
            src_id = _safe(parts, 2)
            tgt_id = _safe(parts, 6)
            if (_looks_player(src_id) or src_id in ctx.pet_ids) and tgt_id == ctx.npc_id:
                return True
        elif opcode in ("26", "30"):
            src_id = _safe(parts, 5)
            tgt_id = _safe(parts, 7)
            if (_looks_player(src_id) or src_id in ctx.pet_ids) and tgt_id == ctx.npc_id:
                return True
        elif opcode == "35":
            src_id = _safe(parts, 2)
            if src_id in ctx.pet_ids:
                return True
    else:
        if opcode == "33":
            source = _safe(parts, 2)
            if not source.startswith("8"):
                return True

    if opcode == "261" and _safe(parts, 2) == "Change":
        interesting, seen = _change_packet_interesting(parts)
        if not interesting:
            for k in seen:
                _DROPPED_CHANGE_KEYS.add(k)
            return True

    return False
