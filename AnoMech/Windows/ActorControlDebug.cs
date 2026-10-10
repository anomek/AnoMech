#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Network;

namespace AnoMech.Windows;

// Test bench for ActorControl categories: feeds one packet to the client's own dispatcher on the
// current target (or self) and logs which actor fields changed, right after and a few frames
// later, so each category can be checked for "does it do anything on our spawns".
internal sealed unsafe class ActorControlDebug
{
    private const uint NoTarget = 0xE0000000;
    private static readonly int[] SnapshotDelays = [0, 30, 120];

    // Arg tokens: "self" = the actor's entity id, "player" = the local player's, "cast" = the
    // action the actor is casting right now.
    private sealed record Preset(string Name, uint Category, params string[] Args);

    private static readonly Preset[] Presets =
    [
        new("0x00 weapon out (1, 1)", 0x00, "1", "1"),
        new("0x00 weapon out, no anim? (1, 0)", 0x00, "1", "0"),
        new("0x00 weapon in (0, 1)", 0x00, "0", "1"),
        new("0x00 weapon in -- untargetable/death (0, 1, 1)", 0x00, "0", "1", "1"),
        new("0x02 SetMode Dead (2, 2) -- fatal hit", 0x02, "2", "2"),
        new("0x02 SetMode Dead (2, 0) -- after death anim", 0x02, "2", "0"),
        new("0x02 SetMode Normal (1, 0)", 0x02, "1", "0"),
        new("0x04 combat (1)", 0x04, "1"),
        new("0x04 out of combat (0)", 0x04, "0"),
        new("0x0E death animation", 0x0E),
        new("0x0F cancel cast -- killed (540, 1, cast, 1)", 0x0F, "540", "1", "cast", "1"),
        new("0x0F cancel cast -- cancelled (537, 1, cast, 0)", 0x0F, "537", "1", "cast", "0"),
        new("0x0F cancel cast -- self-cancel (538, 1, cast, 0)", 0x0F, "538", "1", "cast", "0"),
        new("0x0F cancel cast -- silent (0, 1, cast, 0)", 0x0F, "0", "1", "cast", "0"),
        new("0x22 head marker (lockon, self)", 0x22, "0", "self"),
        new("0x23 tether (0, tetherId, player, 15)", 0x23, "0", "0", "player", "15"),
        new("0x24 spawned-add unknown (1, 142)", 0x24, "1", "142"),
        new("0x24 spawned-add unknown (0, 0)", 0x24, "0", "0"),
        new("0x27 corpse fade", 0x27),
        new("0x2F tether clear", 0x2F),
        new("0x31 ModeAttributeFlags (value)", 0x31, "0x31"),
        new("0x36 targetable (1)", 0x36, "1"),
        new("0x36 untargetable (0)", 0x36, "0"),
        new("0x3F ModelState (value)", 0x3F, "0"),
        new("0x197 action timeline (id)", 0x197, "7737"),
        new("0x25F prop fade (self, 1, 0, 100)", 0x25F, "self", "1", "0", "100"),
        new("0x25F prop fade (self, 5, 3, 100)", 0x25F, "self", "5", "3", "100"),
    ];

    private sealed record PendingSnapshot(int FramesLeft, int Delay, uint EntityId, string Label, Dictionary<string, string> Before);

    private readonly List<PendingSnapshot> pending = [];
    private readonly List<string> results = [];
    private int presetIndex = -1;
    private bool onSelf;
    private string categoryText = "0x36";
    private readonly string[] argTexts = ["1", "0", "0", "0", "0", "0", "0", "0"];
    private string targetIdText = "0xE0000000";

    public void Tick()
    {
        for (var i = pending.Count - 1; i >= 0; i--)
        {
            var p = pending[i];
            if (p.FramesLeft > 0)
            {
                pending[i] = p with { FramesLeft = p.FramesLeft - 1 };
                continue;
            }
            pending.RemoveAt(i);
            var after = Snapshot(p.EntityId);
            Report($"{p.Label} +{p.Delay}f: {(after == null ? "actor gone" : Diff(p.Before, after))}");
        }
    }

    public void Draw()
    {
        var uiScale = ImGuiHelpers.GlobalScale;
        ImGui.TextUnformatted("ActorControl test bench");
        ImGui.Separator();

        var actor = ResolveActor();
        ImGui.Checkbox("On self instead of target##acself", ref onSelf);
        ImGui.SameLine();
        ImGui.TextUnformatted(actor == null ? "(no actor)" : $"-> {Name(actor)} 0x{actor->EntityId:X}");

        ImGui.SetNextItemWidth(360 * uiScale);
        if (ImGui.BeginCombo("Preset##acpreset", presetIndex < 0 ? "(custom)" : Presets[presetIndex].Name))
        {
            for (var i = 0; i < Presets.Length; i++)
            {
                if (!ImGui.Selectable(Presets[i].Name, i == presetIndex)) continue;
                presetIndex = i;
                categoryText = $"0x{Presets[i].Category:X}";
                for (var a = 0; a < argTexts.Length; a++)
                    argTexts[a] = a < Presets[i].Args.Length ? Presets[i].Args[a] : "0";
            }
            ImGui.EndCombo();
        }

        ImGui.SetNextItemWidth(100 * uiScale);
        ImGui.InputText("Category##accat", ref categoryText, 16);
        for (var a = 0; a < argTexts.Length; a++)
        {
            if (a % 4 != 0) ImGui.SameLine();
            ImGui.SetNextItemWidth(80 * uiScale);
            ImGui.InputText($"a{a + 1}##acarg{a}", ref argTexts[a], 16);
        }
        ImGui.SetNextItemWidth(100 * uiScale);
        ImGui.InputText("TargetId##actarget", ref targetIdText, 16);

        if (ImGui.Button("Fire ActorControl##acfire")) Fire(actor);
        ImGui.SameLine();
        if (ImGui.Button("Log state##acstate") && actor != null)
            Report($"state {Name(actor)}: {string.Join(" ", Snapshot(actor->EntityId)!.Select(kv => $"{kv.Key}={kv.Value}"))}");
        ImGui.SameLine();
        if (ImGui.Button("Clear##acclear")) results.Clear();

        foreach (var line in results)
            ImGui.TextWrapped(line);
    }

    private BattleChara* ResolveActor()
    {
        var obj = onSelf ? Plugin.ObjectTable.LocalPlayer : Plugin.TargetManager.Target;
        if (obj == null) return null;
        var go = (GameObject*)obj.Address;
        return go != null && go->IsCharacter() ? (BattleChara*)go : null;
    }

    private void Fire(BattleChara* actor)
    {
        if (actor == null)
        {
            Report("no actor: target something or tick 'On self'");
            return;
        }
        if (!TryParse(categoryText, actor, out var category))
        {
            Report($"can't parse category '{categoryText}'");
            return;
        }
        var args = new uint[argTexts.Length];
        for (var a = 0; a < args.Length; a++)
        {
            if (TryParse(argTexts[a], actor, out args[a])) continue;
            Report($"can't parse a{a + 1} '{argTexts[a]}'");
            return;
        }
        if (!TryParse(targetIdText, actor, out var targetId))
        {
            Report($"can't parse TargetId '{targetIdText}'");
            return;
        }

        var entityId = actor->EntityId;
        var label = $"AC 0x{category:X}({string.Join(", ", args.Reverse().SkipWhile(v => v == 0).Reverse().Select(v => $"0x{v:X}"))}) on {Name(actor)} 0x{entityId:X}";
        var before = Snapshot(entityId)!;
        try
        {
            PacketDispatcher.HandleActorControlPacket(entityId, category, args[0], args[1], args[2], args[3], args[4], args[5], args[6], args[7], targetId, false);
        }
        catch (Exception e)
        {
            Report($"{label} threw: {e.Message}");
            return;
        }
        Report($"{label} sent");
        foreach (var delay in SnapshotDelays)
            pending.Add(new PendingSnapshot(delay, delay, entityId, label, before));
    }

    private void Report(string line)
    {
        Plugin.Log.Info($"[ActorControlDebug] {line}");
        results.Insert(0, line);
        if (results.Count > 30) results.RemoveAt(results.Count - 1);
    }

    private static string Name(BattleChara* actor) => ((GameObject*)actor)->GetName().ToString();

    private static Dictionary<string, string>? Snapshot(uint entityId)
    {
        var obj = Plugin.ObjectTable.SearchByEntityId(entityId);
        if (obj == null) return null;
        var chara = (BattleChara*)obj.Address;
        var go = (GameObject*)chara;
        var timeline = &chara->Timeline;
        var slot0 = timeline->TimelineSequencer.Parent == null ? "-" : timeline->TimelineSequencer.GetSlotTimeline(0).ToString();
        var draw = chara->DrawObject;
        return new Dictionary<string, string>
        {
            ["Targetable"] = $"0x{(byte)go->TargetableStatus:X2}",
            ["Mode"] = $"{chara->Mode}/{chara->ModeParam}",
            ["ModelState"] = $"0x{timeline->ModelState:X2}",
            ["WeaponDrawn"] = timeline->IsWeaponDrawn.ToString(),
            ["OffhandDrawn"] = chara->IsOffhandDrawn.ToString(),
            ["InCombat"] = chara->InCombat.ToString(),
            ["CondInCombat"] = Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat].ToString(),
            ["ModeAttrFlags"] = $"0x{chara->ModelContainer.ModeAttributeFlags:X2}",
            ["BaseOverride"] = timeline->BaseOverride.ToString(),
            ["Slot0Timeline"] = slot0,
            ["Casting"] = chara->CastInfo.IsCasting ? chara->CastInfo.ActionId.ToString() : "no",
            ["Tether0"] = chara->Vfx.Tethers[0].Id.ToString(),
            ["Tether1"] = chara->Vfx.Tethers[1].Id.ToString(),
            ["Health"] = chara->Health.ToString(),
            ["RenderFlags"] = go->RenderFlags.ToString(),
            ["Drawn"] = draw == null ? "no DrawObject" : draw->IsVisible.ToString(),
            ["Transparency"] = draw == null ? "-" : draw->GetTransparency().ToString("F2", CultureInfo.InvariantCulture),
        };
    }

    private static string Diff(Dictionary<string, string> before, Dictionary<string, string> after)
    {
        var changes = after.Where(kv => before.GetValueOrDefault(kv.Key) != kv.Value)
            .Select(kv => $"{kv.Key} {before.GetValueOrDefault(kv.Key)} -> {kv.Value}")
            .ToList();
        return changes.Count == 0 ? "no tracked field changed" : string.Join(", ", changes);
    }

    private static bool TryParse(string input, BattleChara* actor, out uint value)
    {
        var s = input.Trim();
        switch (s.ToLowerInvariant())
        {
            case "self":
                value = actor->EntityId;
                return true;
            case "player":
                value = Plugin.ObjectTable.LocalPlayer?.EntityId ?? NoTarget;
                return true;
            case "cast":
                value = actor->CastInfo.ActionId;
                return true;
        }
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
#endif
