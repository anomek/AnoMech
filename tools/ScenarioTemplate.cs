// ScenarioTemplate — scaffold for `parser.py --code` to emit a runnable IScenario
// from a recorded ACT/IINACT pull. This breaks from the canonical timeline-centric
// pattern used by TopP5Delta/TopP5Sigma: instead of a single chronological list of
// world.Events.Add(...) calls inside Run, every tracked NPC owns a Run_<Name>()
// method that registers its own events. Run() just calls them all.
//
// Why: log-derived scenarios are noisy and per-actor; reading a single 800-line
// Run is unmanageable. Grouping by source actor lets a human skim "what does this
// boss do" or "what did this add do" without grep.
//
// Coordinate convention:
//   IINACT log "PosX|PosY|PosZ" maps to C# Vector3(PosX, PosZ, PosY) — the log's
//   PosY is the north/south axis, PosZ is height. Spawn placements are scenario-local
//   (origin subtracted); MoveTo/Move(Placement) take world coords.
//
// Opcode → API mapping (parser.py emits these; unknown opcodes fall through as
// bare comments — `// <raw packet>`):
//
//   03 AddCombatant             →  field = world.SpawnEnemy(new EnemySpawnConfig(
//                                      BNpcBaseId, NameId, Level, Targetable, InEnemyList,
//                                      Placement: new Placement(<local>, <heading>)))
//
//   04 RemoveCombatant          →  field?.Despawn()
//
//   20 StartsCasting            →  field?.Cast(0x<spellId> /* <spell name> */,
//                                      [targetLocation: <world vec> from 263,]
//                                      castSeconds: <s>)
//   263 ActorCastExtra          →  folded into the preceding 20 cast as targetLocation.
//
//   26 NetworkBuff (apply)      →  <target>?.AddStatus(0x<statusId>, <duration>f[, stacks: N])
//   27 NetworkBuff (remove)     →  comment only (ambiguous in TOP logs)
//
//   35 Tether                   →  world.Tether(<a>, <b>, 0x<tetherId>, <duration>f[, debuffStatusId])
//
//   270 ActorMove               →  field?.MoveTo(new Vector3(<world>, <world>, <world>))
//   271 ActorSetPos             →  field?.Move(new Placement(<world>, <heading>))
//
//   261 ActorChange             →  ModelStatus 0x0     → field?.SetVisible(true)
//                                  ModelStatus 0x4000  → field?.SetVisible(false)
//                                  other deltas        → bare comment
//   273 ActorControlExtra       →  code 0197 param 1E39 → field?.PlayActionTimeline(WarpOut)
//                                  code 0197 param 1E43 → field?.PlayActionTimeline(Spawn)
//                                  other codes          → bare comment
//
//   257 MapEffect               →  world.Map.AddEffect(packetFlags: 0x<flags>, index: 0x<index>)
//
//   33 ActorControl (director)  →  bare comment (informational — engage/clear/timer codes)
//   258/259/267/268/269/272     →  bare comment
//
// Player-role mapping:
//   Players seen in the log (actor ids starting with 1) are assigned PartyRoles in
//   first-seen order: 1st → MainTank, 2nd → OffTank, 3rd → RegenHealer, etc. When a
//   boss event references a player by id, the parser emits `party.Get(PartyRole.<X>)`
//   instead of the raw id. The mapping is stable per pull but arbitrary across pulls.

using System.Collections.Generic;
using System.Numerics;
using UltiSim.Core;
using UltiSim.Core.Map;
using UltiSim.Core.SimObjects;

namespace UltiSim.Scenarios;

public sealed class ScenarioTemplate : IScenario
{
    public string Name => "<Scenario Name>";

    // Fill from the log: TerritoryId via `01|<ts>|<hex zone>|<name>`, Origin/PlayerPosition
    // from a pre-pull `261|Add|<playerId>|...|PosX|PosY|PosZ`, WeatherId from a sibling scenario.
    public TargetInstance? TargetInstance { get; } = new(
        TerritoryId: 0,
        Origin: new Vector3(0f, 0f, 0f),
        PlayerPosition: new Vector3(0f, 0f, 0f),
        WeatherId: 0);

    public IReadOnlyList<ScenarioOriginOverride> OriginOverrides { get; } = [];
    public IReadOnlyList<uint> HiddenBaseIds { get; } = [];
    public IReadOnlyList<Waymark> Waymarks { get; } = [];
    public ushort Bgm => 0;

    private SimWorld world = null!;
    private SimParty party = null!;

    // One field per stable NPC instance. The parser sanitises the BNpc name and
    // suffixes the actor id so collisions are impossible.
    // private SimEnemy? omega_4000A3E8;
    // private SimEnemy? opticalUnit_4000A3E7;

    public void Run(SimWorld worldParam, PartyRole playerRole)
    {
        world = worldParam;
        party = worldParam.Party;
        _ = (world, party, playerRole); // silence "unused" until the parser fills the methods below

        // One call per NPC instance, in any order — each method registers its own
        // world.Events.Add(...) lines, so the timeline is reconstructed by the
        // EventScheduler regardless of registration order.
        // Run_Omega_4000A3E8();
        // Run_OpticalUnit_4000A3E7();

        // Map effects + director codes that don't belong to a single actor.
        // Run_InstanceEvents();
    }

    public void Tick(float delta, float elapsed) { }

    // private void Run_Omega_4000A3E8()
    // {
    //     world.Events.Add(0.10f, () => omega_4000A3E8 = world.SpawnEnemy(new EnemySpawnConfig(
    //         BNpcBaseId: 15708, NameId: 7695, Level: 90, Targetable: true, InEnemyList: true,
    //         Placement: new Placement(new Vector3(0f, 0f, -10f), 0f))));
    //     // 03|4000A3E8|Omega|22|5A|0000|4F|Cactuar|0|7695|15708|8557964|8557964|0|10000|||100.00|90.00|0.00|0.00
    //
    //     world.Events.Add(2.00f, () => omega_4000A3E8?.Cast(0x7B03 /* Program Loop */,
    //         targetLocation: new Vector3(99.93f, 0.00f, 111.96f),
    //         castSeconds: 3.7f));
    //     // 20|4000A3E8|Omega|7B03|Program Loop|4000A3E8|Omega|3.7|...
    //
    //     world.Events.Add(5.40f, () => omega_4000A3E8?.SetTargetable(false));
    //     // 261|Change|4000A3E8|ModelStatus|16384
    //
    //     world.Events.Add(11.10f, () => omega_4000A3E8?.MoveTo(new Vector3(99.99f, -0.02f, 91.99f)));
    //     // 270|4000A3E8|0.0019|0000|005A|99.99|91.99|-0.0153
    //
    //     world.Events.Add(20.50f, () => party.Get(PartyRole.MainTank)?.AddStatus(0x152 /* Forge It */, 18.00f));
    //     // 26|0152|Forge It|18.00|4000A3E8|Omega|108C0704|Kal Mor'kal|...
    // }
    //
    // private void Run_InstanceEvents()
    // {
    //     world.Events.Add(0.50f, () => world.Map.AddEffect(packetFlags: 0x00020001, index: 0x09));
    //     // 257|800375AC|00020001|09
    //
    //     // 33|800375AC|40000003|00|00|00|00          // director clear code — informational
    // }
}
