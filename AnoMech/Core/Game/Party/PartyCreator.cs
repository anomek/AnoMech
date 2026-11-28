using AnoMech.Core.SimObjects;
using System;
using System.Numerics;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Scenarios;

namespace AnoMech.Core.Game.Party;

// Spawns one party seat's doppel. Party members spawn only in an inn-launched zone (see
// Game.ZoneIn), where doppels get CharacterManager registration.
internal static class PartyCreator
{
    private const float RingRadius = 2.5f;

    // The seat's spot on the ring around the scenario origin, facing the centre. No Rng: the party
    // is seated before a run's seed exists.
    public static Placement RingPlacement(PartyRole role)
    {
        var angle = (int)role / 8f * MathF.Tau;
        var local = new Vector3(MathF.Sin(angle) * RingRadius, 0f, MathF.Cos(angle) * RingRadius);
        return new Placement(local, MathF.Atan2(-local.X, -local.Z));
    }

    // The level cap, for a territory the duty sync doesn't cover.
    private const byte UnsyncedLevel = 100;

    public static ISimPartyMember? Spawn(PartySlot slot, SimWorld world, PartyRole role, Placement placement) => slot switch
    {
        BotSlot bot => SpawnBot(bot.Preset, world, role, placement),
        PuppetSlot puppet => SpawnPuppet(puppet.Preset, world, role, placement),
        _ => null,
    };

    // Everyone in the duty is synced to its level, as the player is.
    private static byte Level(SimWorld world) => Natives.Data.DutyLevel(world.Map.TerritoryId) ?? UnsyncedLevel;

    private static SimPartyNpc? SpawnBot(PartyMemberPreset preset, SimWorld world, PartyRole role, Placement placement)
    {
        if (Natives.BattleCharas.SpawnDoppel(preset, Level(world), world.Coordinates.ToGlobal(placement), out var entityId) is not { } chara) return null;

        Plugin.Log.Info($"PartyCreator: spawned {preset.Name} ({role}, job {preset.ClassJob}) at index {chara.Slot}");
        // Bots steer around the scenario's geometry; bosses walk straight lines.
        var member = new SimPartyNpc(chara, world.Coordinates, role, preset.ClassJob, preset.Name, world.Obstacles);
        member.BeginPacketSpawn(entityId, placement);
        return member;
    }

    // Same visuals as a bot; the owner's own client moves it.
    private static SimNetworkPuppet? SpawnPuppet(PartyMemberPreset preset, SimWorld world, PartyRole role, Placement placement)
    {
        if (Natives.BattleCharas.SpawnDoppel(preset, Level(world), world.Coordinates.ToGlobal(placement), out var entityId) is not { } chara) return null;

        Plugin.Log.Info($"PartyCreator: spawned network puppet {preset.Name} ({role}, job {preset.ClassJob}) at index {chara.Slot}");
        var puppet = new SimNetworkPuppet(chara, world.Coordinates, role, preset.ClassJob, preset.Name);
        puppet.BeginPacketSpawn(entityId, placement);
        return puppet;
    }
}
