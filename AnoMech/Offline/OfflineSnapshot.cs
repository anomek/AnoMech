using System;
using System.Collections.Generic;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Common.Configuration;
using CSPlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;
using UserFileEvent = FFXIVClientStructs.FFXIV.Client.UI.Misc.UserFileManager.UserFileEvent;

namespace AnoMech.Offline;

// The title screen's state from before offline mode wrote anything, put back once the game has
// logged out of the offline world, so the next real login starts as if offline mode never ran.
// Only plain data is copied back; what holds pointers, the game's own logout resets and the next
// login reloads, exactly as when switching characters.
internal sealed unsafe class OfflineSnapshot
{
    // The word SetCurrentLevel sets (see its signature in OfflineBootstrap).
    private const int LevelFlagsOffset = 0x910;

    private readonly record struct Setting(nint Entry, uint UInt, float Float, string? Text);

    private readonly byte[] player;
    private readonly ushort levelFlags;
    private readonly byte[] unlockLinks;
    private readonly byte[] completedQuests;
    private readonly byte[] conditions;
    private readonly byte[] equipped;
    private readonly bool equippedLoaded;
    private readonly uint localEntityId;
    private readonly List<Setting> settings = [];
    private string? sharedMacrosInWorld;

    private OfflineSnapshot(CSPlayerState* state, UIState* ui, QuestManager* quests, Conditions* flags, InventoryContainer* container, Control* control, SystemConfig* config)
    {
        // Everything before the first container is plain data.
        var plain = (int)((byte*)&state->CompletedCrystariumDeliveryQuests - (byte*)state);
        player = new ReadOnlySpan<byte>(state, plain).ToArray();
        levelFlags = *(ushort*)((byte*)state + LevelFlagsOffset);
        unlockLinks = ui->UnlockLinks.ToArray();
        completedQuests = quests->CompletedQuests.ToArray();
        conditions = new ReadOnlySpan<byte>(flags, sizeof(Conditions)).ToArray();
        equipped = container->Items == null ? [] : new ReadOnlySpan<byte>(container->Items, container->Size * sizeof(InventoryItem)).ToArray();
        equippedLoaded = container->IsLoaded;
        localEntityId = control->LocalPlayerEntityId;
        Capture(&config->ConfigBase);
        Capture(&config->UiConfig);
        Capture(&config->UiControlConfig);
        Capture(&config->UiControlGamepadConfig);
    }

    public static OfflineSnapshot? Take(out string why)
    {
        why = "";
        var state = CSPlayerState.Instance();
        var ui = UIState.Instance();
        var quests = QuestManager.Instance();
        var flags = Conditions.Instance();
        var inventory = InventoryManager.Instance();
        var container = inventory == null ? null : inventory->GetInventoryContainer(InventoryType.EquippedItems);
        var control = Control.Instance();
        var framework = Framework.Instance();
        if (state == null || ui == null || quests == null || flags == null || container == null || control == null || framework == null)
        {
            why = "a game system offline mode puts back afterwards isn't available";
            return null;
        }
        return new OfflineSnapshot(state, ui, quests, flags, container, control, &framework->SystemConfig.SystemConfigBase);
    }

    private void Capture(ConfigBase* config)
    {
        if (config->ConfigEntry == null) return;
        for (var i = 0u; i < config->ConfigCount; i++)
        {
            var entry = config->ConfigEntry + i;
            switch ((ConfigType)entry->Type)
            {
                case ConfigType.UInt:
                    settings.Add(new Setting((nint)entry, entry->Value.UInt, 0, null));
                    break;
                case ConfigType.Float:
                    settings.Add(new Setting((nint)entry, 0, entry->Value.Float, null));
                    break;
                case ConfigType.String:
                    settings.Add(new Setting((nint)entry, 0, 0, Text(entry)));
                    break;
            }
        }
    }

    private static string? Text(ConfigEntry* entry) => entry->Value.String == null ? null : entry->Value.String->ToString();

    // Shared macros are the one thing that can be edited offline and saved outside a character's
    // own files; offline mode can't put back an edit, so it compares what the world started with.
    public void NoteWorldEntered() => sharedMacrosInWorld = SharedMacros();

    public bool SharedMacrosChanged() => sharedMacrosInWorld != null && SharedMacros() != sharedMacrosInWorld;

    private static string? SharedMacros()
    {
        var macros = RaptureMacroModule.Instance();
        if (macros == null) return null;
        var text = new StringBuilder();
        foreach (ref var macro in macros->Shared)
        {
            text.Append(macro.IconId).Append('\u001f').Append(macro.MacroIconRowId).Append('\u001f').Append(macro.Name.ToString());
            foreach (ref var line in macro.Lines) text.Append('\u001f').Append(line.ToString());
            text.Append('\u001e');
        }
        return text.ToString();
    }

    public bool Restore(out string problem)
    {
        problem = "";
        var state = CSPlayerState.Instance();
        var ui = UIState.Instance();
        var quests = QuestManager.Instance();
        var flags = Conditions.Instance();
        var inventory = InventoryManager.Instance();
        var container = inventory == null ? null : inventory->GetInventoryContainer(InventoryType.EquippedItems);
        var control = Control.Instance();
        if (state == null || ui == null || quests == null || flags == null || container == null || control == null)
        {
            problem = "a game system went missing";
            return false;
        }
        if (ui->UnlockLinks.Length != unlockLinks.Length || quests->CompletedQuests.Length != completedQuests.Length
            || (container->Items == null ? 0 : container->Size * sizeof(InventoryItem)) != equipped.Length)
        {
            problem = "the game's own state changed shape";
            return false;
        }

        player.CopyTo(new Span<byte>(state, player.Length));
        *(ushort*)((byte*)state + LevelFlagsOffset) = levelFlags;
        unlockLinks.CopyTo(ui->UnlockLinks);
        completedQuests.CopyTo(quests->CompletedQuests);
        conditions.CopyTo(new Span<byte>(flags, conditions.Length));
        if (equipped.Length > 0) equipped.CopyTo(new Span<byte>(container->Items, equipped.Length));
        container->IsLoaded = equippedLoaded;
        control->LocalPlayerEntityId = localEntityId;

        var changed = 0;
        foreach (var setting in settings)
        {
            var entry = (ConfigEntry*)setting.Entry;
            switch ((ConfigType)entry->Type)
            {
                case ConfigType.UInt when entry->Value.UInt != setting.UInt:
                    entry->SetValueUInt(setting.UInt);
                    changed++;
                    break;
                case ConfigType.Float when BitConverter.SingleToInt32Bits(entry->Value.Float) != BitConverter.SingleToInt32Bits(setting.Float):
                    entry->SetValueFloat(setting.Float);
                    changed++;
                    break;
                case ConfigType.String when setting.Text != null && Text(entry) != setting.Text:
                    // The setter destroys the string it is given; only its allocation is left to free.
                    var text = Utf8String.FromString(setting.Text);
                    entry->SetValueString(text);
                    IMemorySpace.Free(text);
                    changed++;
                    break;
            }
        }

        // Anything a module marked for saving offline belongs to no character and is dropped.
        Settle(RaptureHotbarModule.Instance() is var hotbars && hotbars != null ? &hotbars->UserFileEvent : null);
        Settle(RaptureMacroModule.Instance() is var macros && macros != null ? &macros->UserFileEvent : null);
        Settle(RaptureGearsetModule.Instance() is var gearsets && gearsets != null ? &gearsets->UserFileEvent : null);
        Settle(AddonConfig.Instance() is var addons && addons != null ? &addons->UserFileEvent : null);
        Settle(UIInputData.Instance() is var input && input != null ? &input->UserFileEvent : null);

        OfflineTrace.Write($"[Offline] Put back the title screen's state ({changed} settings changed back).");
        return true;
    }

    private static void Settle(UserFileEvent* module)
    {
        if (module == null || module->CharacterContentId != 0) return;
        module->HasChanges = false;
        module->IsSavePending = false;
    }
}
