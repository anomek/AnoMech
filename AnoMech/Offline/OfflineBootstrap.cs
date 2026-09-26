using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using CSPlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;

namespace AnoMech.Offline;

internal sealed unsafe class OfflineBootstrap : IDisposable
{
    public enum Result { Working, Done, Failed }

    private enum Stage { Preflight, LoadConfig, EnterGameUi, WaitHud, Player, ZoneIn, WaitLayout, Spawn, WaitWarp, Finish }

    public const uint LocalEntityId = 0x10FFA001;
    // Never a real character's id, so nothing the game keys on it can reach a real character's files.
    private const ulong SandboxContentId = 0x00FFA0FF00000001;
    private const uint InvalidObjectId = 0xE0000000;
    // ContentsReplayManager's "re-zoning locally" bit: the zone init skips its server notification
    // and enter-territory events, whose EventPlay would never arrive.
    private const byte ReZoningFlag = 0x10;
    // AgentLobby's idle UI stage; the title's own stage reopens the title menu after a minute.
    private const byte LobbyIdleUiStage = 1;
    private const byte TitleUpdateStage = 1;
    private const byte PlayerSubKind = 4;
    private const uint ZoneInActorControl = 200;
    private const uint TerritoryLoaded = 2;
    private const uint TerritoryUnloading = 3;
    private const uint DisplayHatHidden = 0x40, DisplayWeaponHidden = 0x80, DisplayVisor = 0x800;
    private const ushort FullCondition = 30000;

    private static readonly TimeSpan HudTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LayoutTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan WarpTimeout = TimeSpan.FromSeconds(75);

    private const string SetClassJobIdSignature = "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 0F B6 F2 48 8B D9 88 51 7E";
    private const string SetCurrentLevelSignature = "66 89 91 88 00 00 00 66 C7 81 10 09 00 00 01 01 C3";
    private const string GaugePacketSignature = "4C 8B 01 4D 85 C0 74 ?? 0F B6 02 38 41 58";
    // The lobby's scene update, through a caller's call so a plugin hook on it can't hide it.
    private const string LobbySceneSignature = "E8 ?? ?? ?? ?? 80 BF ?? ?? ?? ?? ?? 48 8D 35";
    private const int NoLobbyScene = -1;
    // ConfigModule's completion of a character's settings load, the only place the game derives what
    // depends on the settings as a whole (name plate colours, camera zoom limit, target lines). Its
    // first check is the requester at +0x40 it would answer.
    private const string SettingsLoadedSignature = "40 57 48 81 EC ?? ?? ?? ?? 48 83 79 40 00 48 8B F9 0F 84 ?? ?? ?? ?? 85 D2 0F 84 ?? ?? ?? ?? 83 EA 03";
    private const int SettingsLoadRequesterOffset = 0x40;
    private const int SettingsLoadSucceeded = 0;

    private delegate void SetClassJobIdDelegate(CSPlayerState* state, byte classJobId);
    private delegate void SetCurrentLevelDelegate(CSPlayerState* state, short level);
    private delegate void GaugePacketDelegate(JobGaugeManager* manager, byte* packet);
    private delegate byte LobbySceneDelegate(int scene, int time);
    private delegate void SettingsLoadedDelegate(ConfigModule* module, int result);

    private readonly OfflineSession session;
    private readonly OfflineNetworkGuard guard = new();
    private readonly OfflineFileGuard files = new();
    private readonly OfflineConnectionGuard connections = new();
    private Stage stage = Stage.Preflight;
    private long stageStarted = Stopwatch.GetTimestamp();
    private SetClassJobIdDelegate? setClassJobId;
    private SetCurrentLevelDelegate? setCurrentLevel;
    private GaugePacketDelegate? gaugePacket;
    private LobbySceneDelegate? lobbyScene;
    private SettingsLoadedDelegate? settingsLoaded;
    private OfflineLoadout loadout = null!;
    private OfflineInnRoom inn = null!;
    private byte weatherId = 1;
    private bool setReZoning;

    public OfflineBootstrap(OfflineSession session)
    {
        this.session = session;
        Menus = new OfflineMenuGuard(session);
    }

    public OfflineMenuGuard Menus { get; }

    public bool RearmGuards(out string why) => files.Arm(out why) && guard.Arm(out why) && connections.Arm(out why);

    public bool ChangedGameState => stage > Stage.Preflight;
    public OfflineSnapshot? Snapshot { get; private set; }
    // Once the scene has changed, only the game's own logout brings the title screen back.
    public bool EnteredGameUi { get; private set; }

    public void ClearReZoning()
    {
        if (!setReZoning) return;
        var replay = &GameMain.Instance()->ContentsReplayManager;
        replay->PlaybackControls = (ContentsReplayPlaybackControl)((byte)replay->PlaybackControls & ~ReZoningFlag);
        setReZoning = false;
    }

    public void Dispose()
    {
        Menus.Dispose();
        guard.Dispose();
        files.Dispose();
        connections.Dispose();
    }

    private TimeSpan InStage => Stopwatch.GetElapsedTime(stageStarted);

    public Result Step(out string status)
    {
        return stage switch
        {
            Stage.Preflight => Preflight(out status),
            Stage.LoadConfig => LoadConfig(out status),
            Stage.EnterGameUi => EnterGameUi(out status),
            Stage.WaitHud => WaitHud(out status),
            Stage.Player => Player(out status),
            Stage.ZoneIn => ZoneIn(out status),
            Stage.WaitLayout => WaitLayout(out status),
            Stage.Spawn => Spawn(out status),
            Stage.WaitWarp => WaitWarp(out status),
            _ => Finish(out status),
        };
    }

    private void Advance(Stage next)
    {
        stage = next;
        stageStarted = Stopwatch.GetTimestamp();
        OfflineTrace.Write(OfflineProbe.Describe($"entering {next}"));
    }

    private Result Fail(out string status, string reason)
    {
        status = reason;
        OfflineTrace.Write(OfflineProbe.Describe($"FAILED in {stage}: {reason}"));
        return Result.Failed;
    }

    private static RaptureAtkModule* AtkModule()
    {
        var ui = UIModule.Instance();
        return ui == null ? null : ui->GetRaptureAtkModule();
    }

    private Result Preflight(out string status)
    {
        OfflineTrace.Begin();
        OfflineTrace.Write(OfflineProbe.Describe("preflight"));
        status = "Checking the game...";

        if (session.Loadout is not { } chosen)
            return Fail(out status, "No character is selected.");
        loadout = chosen;
        inn = chosen.Inn;

        if (OfflineGameFiles.FileModulesBusy() is { } busy)
            return Fail(out status, busy);

        var framework = Framework.Instance();
        if (framework == null || framework->NetworkModuleProxy == null || framework->NetworkModuleProxy->NetworkModule == null)
            return Fail(out status, "The game's network layer isn't ready yet. Wait a moment at the title screen and try again.");
        if (OfflineProbe.ZoneClient() != 0)
            return Fail(out status, "The game is still connected to a server. Wait a moment at the title screen and try again.");

        var game = GameMain.Instance();
        var control = Control.Instance();
        var atk = AtkModule();
        var lobby = AgentLobby.Instance();
        var config = ConfigModule.Instance();
        if (game == null || control == null || atk == null || lobby == null || config == null || CSPlayerState.Instance() == null || WarpInfo.Instance() == null)
            return Fail(out status, "A game system offline mode needs isn't available.");
        if (*(nint*)((byte*)config + SettingsLoadRequesterOffset) != 0)
            return Fail(out status, "The game is still loading settings. Wait a moment at the title screen and try again.");
        if (game->TerritoryLoadState == TerritoryUnloading || control->LocalPlayer != null)
            return Fail(out status, "A character is still loaded. Wait a moment at the title screen and try again.");
        if (atk->UIScene != GameUIScene.LobbyMain || lobby->LobbyUpdateStage != TitleUpdateStage)
            return Fail(out status, "Go back to the title screen to start offline mode.");
        if (lobby->LobbyUIStage != OfflineSession.TitleUiStage)
            return Fail(out status, "Wait until the title screen is idle, with none of its menus open.");

        if (Plugin.DataManager.GetExcelSheet<TerritoryType>().GetRowOrDefault(inn.TerritoryId) is not { } territory)
            return Fail(out status, "The inn room couldn't be read from the game data.");
        weatherId = FirstWeather(territory);
        if (session.UseConfig && !OfflineGameFiles.CanLoadConfig(out var configWhy))
            return Fail(out status, $"Your configuration can't be loaded on this game version ({configWhy}). Turn off \"Use my hotbars, keybinds, HUD layout and settings\" to go offline without it.");

        try
        {
            setClassJobId = Marshal.GetDelegateForFunctionPointer<SetClassJobIdDelegate>(Plugin.SigScanner.ScanText(SetClassJobIdSignature));
            setCurrentLevel = Marshal.GetDelegateForFunctionPointer<SetCurrentLevelDelegate>(Plugin.SigScanner.ScanText(SetCurrentLevelSignature));
            gaugePacket = Marshal.GetDelegateForFunctionPointer<GaugePacketDelegate>(Plugin.SigScanner.ScanText(GaugePacketSignature));
            lobbyScene = Marshal.GetDelegateForFunctionPointer<LobbySceneDelegate>(Plugin.SigScanner.ScanText(LobbySceneSignature));
            settingsLoaded = Marshal.GetDelegateForFunctionPointer<SettingsLoadedDelegate>(Plugin.SigScanner.ScanText(SettingsLoadedSignature));
        }
        catch (Exception e)
        {
            return Fail(out status, $"This game version isn't supported by offline mode (a game function wasn't found: {e.Message}).");
        }

        Snapshot = OfflineSnapshot.Take(out var snapshotWhy);
        if (Snapshot == null)
            return Fail(out status, $"This game version isn't supported by offline mode ({snapshotWhy}).");

        if (!RearmGuards(out var why))
            return Fail(out status, $"This game version isn't supported by offline mode ({why}).");

        OfflineTrace.Write($"[Offline] Preflight passed: {loadout.Character.Key}, job {loadout.ClassJobId} level {loadout.Level}, inn {inn.TerritoryId} at {inn.Position}, weather {weatherId}.");
        Advance(Stage.LoadConfig);
        return Result.Working;
    }

    // Before the interface exists, as on a login, so the HUD is built from the loaded layout.
    private Result LoadConfig(out string status)
    {
        status = "Loading your configuration...";
        OfflineProcessState.Tainted = true;
        if (session.UseConfig) OfflineGameFiles.LoadConfig(loadout.Character.Folder, OfflineTrace.Write);
        else OfflineGameFiles.LoadDefaults(OfflineTrace.Write);
        settingsLoaded!(ConfigModule.Instance(), SettingsLoadSucceeded);
        OfflineTrace.Write("[Offline] Finished the settings load.");
        Advance(Stage.EnterGameUi);
        return Result.Working;
    }

    private Result EnterGameUi(out string status)
    {
        status = "Loading the game interface...";
        var lobby = AgentLobby.Instance();
        // Start on the title screen can land after the preflight.
        if (lobby->LobbyUIStage != OfflineSession.TitleUiStage || lobby->LobbyUpdateStage != TitleUpdateStage)
            return Fail(out status, "The title screen moved on before offline mode could start.");
        lobby->LobbyUIStage = LobbyIdleUiStage;
        lobby->IdleTime = 0;
        EnteredGameUi = true;
        // As the game's own world entry does before it switches scenes. Otherwise the lobby still
        // reports the title scene, and a plugin that rebuilds that scene on every scene load (Title
        // Edit, housing included) rebuilds it during the inn's zone change.
        lobbyScene!(NoLobbyScene, 0);
        OfflineTrace.Write("[Offline] Left the lobby scene.");
        var changed = AtkModule()->ChangeUIScene(GameUIScene.GameMain);
        OfflineTrace.Write($"[Offline] ChangeUIScene(GameMain) returned {changed}.");
        Advance(Stage.WaitHud);
        return Result.Working;
    }

    private Result WaitHud(out string status)
    {
        status = "Loading the game interface...";
        var atk = AtkModule();
        if (atk->UIScene == GameUIScene.GameMain && atk->AtkModule.EnableUiInput && atk->AtkModule.IsHudInitialized)
        {
            Advance(Stage.Player);
            return Result.Working;
        }
        return InStage > HudTimeout ? Fail(out status, "The game interface didn't finish loading.") : Result.Working;
    }

    private Result Player(out string status)
    {
        status = "Setting up your character...";
        var state = CSPlayerState.Instance();

        state->IsLoaded = false;
        state->EntityId = LocalEntityId;
        state->ContentId = SandboxContentId;
        WriteString(state->CharacterName, loadout.Name);
        state->Race = loadout.Customize[0];
        state->Sex = loadout.Customize[1];
        state->Tribe = loadout.Customize[4];
        state->MaxLevel = loadout.Level;
        state->MaxExpansion = OfflineLoadouts.LatestExpansion();

        var levels = state->ClassJobLevels;
        levels.Clear();
        if (OfflineLoadouts.JobRow(loadout.ClassJobId) is { ExpArrayIndex: >= 0 } row && row.ExpArrayIndex < levels.Length)
            levels[row.ExpArrayIndex] = loadout.Level;

        var attributes = state->Attributes;
        for (var i = 0; i < Math.Min(attributes.Length, loadout.Attributes.Length); i++)
            attributes[i] = loadout.Attributes[i];

        state->SyncedLevel = 0;
        state->IsLevelSynced = false;
        setClassJobId!(state, loadout.ClassJobId);
        setCurrentLevel!(state, loadout.Level);
        state->IsLoaded = true;

        Equip();
        UnlockEverything();

        Advance(Stage.ZoneIn);
        return Result.Working;
    }

    // The inventory a login receives; item level sync and the character window read it.
    private void Equip()
    {
        var set = loadout.Equipment;
        var inventory = InventoryManager.Instance();
        var container = inventory == null ? null : inventory->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null || container->Items == null)
        {
            OfflineTrace.Write("[Offline] The equipped-items container isn't allocated; nothing is equipped.");
            return;
        }
        var equipped = 0;
        for (var slot = 0; slot < Math.Min(container->Size, OfflineGearset.SlotCount); slot++)
        {
            ref var item = ref container->Items[slot];
            var id = set.Items[slot];
            item.Container = InventoryType.EquippedItems;
            item.Slot = (short)slot;
            item.IsSymbolic = false;
            item.ItemId = id % OfflineLoadouts.HqItemOffset;
            item.Quantity = id == 0 ? 0 : 1;
            item.Flags = id >= OfflineLoadouts.HqItemOffset ? InventoryItem.ItemFlags.HighQuality : InventoryItem.ItemFlags.None;
            item.Condition = FullCondition;
            item.SpiritbondOrCollectability = 0;
            item.CrafterContentId = 0;
            item.GlamourId = set.Glamours[slot];
            item.Stains[0] = set.Stain0[slot];
            item.Stains[1] = set.Stain1[slot];
            for (var m = 0; m < OfflineGearset.MateriaPerSlot; m++)
            {
                item.Materia[m] = set.Materia[slot * OfflineGearset.MateriaPerSlot + m];
                item.MateriaGrades[m] = set.MateriaGrades[slot * OfflineGearset.MateriaPerSlot + m];
            }
            if (id != 0) equipped++;
        }
        container->IsLoaded = true;
        OfflineTrace.Write($"[Offline] Equipped {equipped} items.");
    }

    // The server sends these at login and no local file has them; with every bit set, the job can
    // use every action its level allows.
    private static void UnlockEverything()
    {
        var ui = UIState.Instance();
        var quests = QuestManager.Instance();
        if (ui == null || quests == null)
        {
            OfflineTrace.Write("[Offline] Unlocks aren't available; actions learned from quests can't be used.");
            return;
        }
        ui->UnlockLinks.Fill(0xFF);
        quests->CompletedQuests.Fill(0xFF);
        OfflineTrace.Write("[Offline] Unlocked every action and quest.");
    }

    private Result ZoneIn(out string status)
    {
        status = "Loading the inn...";
        var replay = &GameMain.Instance()->ContentsReplayManager;
        if (((byte)replay->PlaybackControls & ReZoningFlag) == 0)
        {
            replay->PlaybackControls = (ContentsReplayPlaybackControl)((byte)replay->PlaybackControls | ReZoningFlag);
            setReZoning = true;
        }

        var packet = new ZoneInitPacket
        {
            TerritoryTypeId = (ushort)inn.TerritoryId,
            WeatherId = weatherId,
            PositionX = inn.Position.X,
            PositionY = inn.Position.Y,
            PositionZ = inn.Position.Z,
        };
        OfflineTrace.Write($"[Offline] HandleZoneInitPacket(0x{LocalEntityId:X}, territory {inn.TerritoryId}, weather {weatherId}).");
        PacketDispatcher.HandleZoneInitPacket(LocalEntityId, &packet, 0);
        // The zone init takes the local entity id from the zone connection, which offline has none.
        Control.Instance()->LocalPlayerEntityId = LocalEntityId;

        Advance(Stage.WaitLayout);
        return Result.Working;
    }

    private Result WaitLayout(out string status)
    {
        status = "Loading the inn...";
        if (GameMain.Instance()->TerritoryLoadState == TerritoryLoaded)
        {
            Advance(Stage.Spawn);
            return Result.Working;
        }
        return InStage > LayoutTimeout ? Fail(out status, "The inn didn't finish loading.") : Result.Working;
    }

    private Result Spawn(out string status)
    {
        status = "Entering the inn...";
        var packet = BuildSpawn();
        OfflineTrace.Write("[Offline] HandleSpawnPlayerPacket.");
        PacketDispatcher.HandleSpawnPlayerPacket(LocalEntityId, &packet);
        if (Control.Instance()->LocalPlayer == null)
            return Fail(out status, "The game didn't create your character.");

        var gauge = stackalloc byte[32];
        new Span<byte>(gauge, 32).Clear();
        gauge[0] = loadout.ClassJobId;
        gaugePacket!(&GameMain.Instance()->JobGaugeManager, gauge);
        AnnounceJob();

        Advance(Stage.WaitWarp);
        return Result.Working;
    }

    private Result WaitWarp(out string status)
    {
        status = "Entering the inn...";
        var warpState = *(uint*)WarpInfo.Instance();
        var zoning = Plugin.Condition[ConditionFlag.BetweenAreas] || Plugin.Condition[ConditionFlag.BetweenAreas51];
        if (warpState == 0 && !zoning && Plugin.ObjectTable.LocalPlayer != null)
        {
            Advance(Stage.Finish);
            return Result.Working;
        }
        return InStage > WarpTimeout ? Fail(out status, "The inn loaded but the loading screen never finished.") : Result.Working;
    }

    // A login's job change, which among other things puts the job's own bars on the hotbars.
    private void AnnounceJob()
    {
        var ui = UIModule.Instance();
        var hotbars = RaptureHotbarModule.Instance();
        if (ui == null || hotbars == null || hotbars->UserFileEvent.CharacterContentId != 0) return;
        ui->HandlePacket(UIModulePacketType.ClassJobChange, loadout.ClassJobId, null);
        hotbars->UserFileEvent.HasChanges = false;
        hotbars->UserFileEvent.IsSavePending = false;
        OfflineTrace.Write($"[Offline] Announced job {loadout.ClassJobId}.");
    }

    private Result Finish(out string status)
    {
        status = "Offline mode";
        ClearReZoning();
        PacketDispatcher.HandleActorControlPacket(LocalEntityId, ZoneInActorControl, 0, 0, 0, 0, 0, 0, 0, 0, InvalidObjectId, false);
        // A server's Condition packet normally sets this; without it nothing does.
        Conditions.Instance()->Normal = true;
        Snapshot?.NoteWorldEntered();
        Menus.Arm();
        OfflineTrace.Write(OfflineProbe.Describe("in the offline world"));
        return Result.Done;
    }

    private SpawnPlayerPacket BuildSpawn()
    {
        var look = loadout.Look;
        var packet = new SpawnPlayerPacket { ContentId = SandboxContentId };
        ref var common = ref packet.Common;
        common.TargetId = InvalidObjectId;
        common.CombatTaggerId = InvalidObjectId;
        common.OwnerId = InvalidObjectId;
        common.TetherTargetId = InvalidObjectId;
        common.MainhandWeaponModel = new WeaponModelId { Value = look.MainHand };
        common.OffhandWeaponModel = new WeaponModelId { Value = look.OffHand };
        common.MaxHealthPoints = loadout.MaxHp;
        common.HealthPoints = loadout.MaxHp;
        common.MaxResourcePoints = (ushort)Math.Min(loadout.MaxMp, ushort.MaxValue);
        common.ResourcePoints = common.MaxResourcePoints;
        common.DisplayFlags = (look.HatHidden ? DisplayHatHidden : 0) | (look.WeaponHidden ? DisplayWeaponHidden : 0) | (look.VisorToggled ? DisplayVisor : 0);
        common.Rotation = (ushort)Math.Round((inn.Rotation + MathF.PI) / (2 * MathF.PI) * ushort.MaxValue);
        common.SpawnIndex = 0;
        common.CharacterMode = CharacterModes.Normal;
        common.ObjectKind = ObjectKind.Pc;
        common.SubKind = PlayerSubKind;
        common.VoiceId = loadout.Voice;
        common.Level = loadout.Level;
        common.ClassJobId = loadout.ClassJobId;
        common.Position = inn.Position;
        for (var slot = 0; slot < 10; slot++)
        {
            var model = look.Equipment[slot];
            common.EquipmentModelIds[slot] = new LegacyEquipmentModelId { Id = (ushort)model, Variant = (byte)(model >> 16), Stain = (byte)(model >> 24) };
            common.ModelStain2Ids[slot] = (byte)(model >> 32);
        }
        common.GlassesIds[0] = look.Glasses[0];
        common.GlassesIds[1] = look.Glasses[1];
        WriteString(common.Name, loadout.Name);
        loadout.Customize.CopyTo(common.CustomizeData.Data);
        return packet;
    }

    private static void WriteString(Span<byte> target, string value)
    {
        target.Clear();
        var bytes = Encoding.UTF8.GetBytes(value);
        var length = bytes.Length;
        if (length > target.Length - 1)
        {
            length = target.Length - 1;
            while (length > 0 && (bytes[length] & 0xC0) == 0x80) length--;
        }
        bytes.AsSpan(0, length).CopyTo(target);
    }

    private static byte FirstWeather(TerritoryType territory)
    {
        if (territory.WeatherRate.ValueNullable is not { } rate) return 1;
        for (var i = 0; i < rate.Weather.Count; i++)
            if (rate.Weather[i].RowId is > 0 and < 256 && rate.Rate[i] > 0) return (byte)rate.Weather[i].RowId;
        return 1;
    }
}
