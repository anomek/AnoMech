using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AnoMech.Core;

// Helpers for synthesizing server-style ActionEffect packets locally so the
// engine's full action-application pipeline runs even when the firewall has
// dropped the real server reply. The Header / TargetEffects / Receive dance
// is intricate enough that we centralize it here; SimEnemy keeps its own
// CastInfo-driven variant for boss cast completion (the inputs differ enough
// that a one-size helper would be more confusing than helpful).
internal static unsafe class ActionEffects
{
    // Monotonically increasing across all synthetic effects so the engine's
    // GlobalSequence dedup doesn't drop our packets. Shared by SimEnemy's
    // cast-finish path and SimPlayer's self-buff path.
    internal static uint NextGlobalSequence = 1;

    // Mimic a server ActionEffect that has the caster apply a status to
    // themselves (e.g. Sprint). Effect.Type = 14 (ApplyStatusEffectTarget);
    // Param2 carries the magnitude the engine reads to size the modifier
    // (e.g. 30 for Sprint's +30% MS — decoded from a real packet in
    // logs/pulls/TOP_pull_05_clear.log); Value carries the status id.
    internal static void FireSelfStatus(
        BattleChara* caster, uint actionId, ushort statusId, byte param)
    {
        if (caster == null) return;

        var selfId = caster->GetGameObjectId();
        var rotationInt = (ushort)Math.Clamp(
            (int)((caster->Rotation / MathF.PI) * 32767f + 32767f), 0, 65535);

        var header = default(ActionEffectHandler.Header);
        header.AnimationTargetId  = selfId;
        header.ActionId           = actionId;
        header.GlobalSequence     = NextGlobalSequence++;
        header.AnimationLock      = 0f;
        header.BallistaEntityId   = 0xE0000000;
        header.SourceSequence     = 0;
        header.RotationInt        = rotationInt;
        header.SpellId            = (ushort)actionId;
        header.AnimationVariation = 0;
        header.ActionType         = (byte)ActionType.Action;
        header.NumTargets         = 1;
        header.ForceAnimationLock = false;

        var effects = default(ActionEffectHandler.TargetEffects);
        effects.Effects[0].Type   = 14;
        effects.Effects[0].Param2 = param;
        effects.Effects[0].Value  = statusId;

        var pos = new Vector3(caster->Position.X, caster->Position.Y, caster->Position.Z);
        var target = selfId;
        ActionEffectHandler.Receive(
            caster->EntityId, (Character*)caster, &pos, &header, &effects, &target);
    }
}
