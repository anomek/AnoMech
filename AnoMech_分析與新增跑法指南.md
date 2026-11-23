# AnoMech 原始碼分析與新增跑法指南

分析日期：2026-11-04（日本時間）
來源：https://github.com/anomek/AnoMech/tree/master
下載位置：D:\FF14\Mods\AnoMech
分析版本：1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade
範圍：下載 master 原始碼與 Git 歷史，分析架構與擴充入口；沒有安裝、啟用插件或修改機制程式。

## 1. 這個專案在做什麼

AnoMech 是 C# Dalamud 插件，用遊戲客戶端模擬 FF14 副本機制，讓玩家在旅館進入模擬場景練習。它不是 Penumbra 的 pmp/ttmp2 素材包，也不是可直接雙擊啟動的獨立模擬器。把原始碼放在 Mods 資料夾不等於已安裝插件。

它建立模擬隊員與 Boss，安排移動、讀條、狀態、連線、攻擊判定及特效。README 的使用入口是旅館內 /anomech，正式安裝說明另見 https://github.com/anomek/MyDalamudPlugins 。本次沒有執行該安裝流程。

README 明確說明模擬區內會阻擋來自伺服器的流量；隊伍進出與 ready check 在模擬期間不會正常更新。這是使用方式的重要限制。

## 2. 架構與入口

下列路徑均相對於本儲存庫：

| 路徑 | 職責 |
|---|---|
| AnoMech/Plugin.cs | Dalamud 服務、指令、更新循環、區域事件 |
| AnoMech/Core/Game/Game.cs | 模擬生命週期、暫停、速度、死亡處理 |
| AnoMech/Core/Game/EventScheduler.cs | 按時間派發事件 |
| AnoMech/Core/Game/ScenarioSession.cs | 單次機制執行 |
| AnoMech/Core/SimObjects/ | 模擬世界、角色、Boss、狀態、連線等 |
| AnoMech/Core/Game/Ai/ | 機器人站位、角色分配、交換與移動 |
| AnoMech/Core/EnemyActions/ | Boss 攻擊與效果 |
| AnoMech/Core/UserActions/ | 玩家技能、職業資源與動作模擬 |
| AnoMech/Core/Native/ | 遊戲客戶端互動邊界及實作 |
| AnoMech/Scenarios/ | 各副本階段、機制、跑法與設定 |
| AnoMech/Multiplayer/ | 多人同步、封包重播與連線 |
| AnoMech.Relay/、AnoMech.Relay.Host/ | 中繼協定、伺服器及獨立主程式 |
| tests/ | NUnit 邏輯與多人測試 |
| tools/parser.py | ACT/IINACT 日誌解析與機制基底產生工具 |

已核對的每幀呼叫順序：
Plugin.OnFrameworkUpdate → Natives.Tick → Runs.Tick → Game.Tick → Multiplayer.Tick。
Game.Tick 在未暫停時依次執行 Events、World、ScenarioSession，並套用 EventTimeScale。

原始碼的 ScenarioCatalog.Create 登錄 25 個機制項目，分布於 Umad、Fru、Top、Uwu、Ucob。登錄存在不代表每個機制都已具備完整 AI 或判定。

## 3. 新增「既有機制的另一種跑法」

主要新增 *Ai.cs，並在對應 *Scenario.cs 的 AiStrats 清單加入實例。不必為同一機制另寫 Boss 時間軸，也通常不必修改 ScenarioCatalog。

可直接參照：
- AnoMech/Scenarios/Top/P2PartySynergy/TopP2PartySynergyScenario.cs
- 同資料夾 TopP2PartySynergyAi.cs
- 同資料夾 TopP2PartySynergyLpduAi.cs
- AnoMech/Core/Game/Ai/IScenarioAi.cs

TOP P2 目前的註冊：
    public IReadOnlyList<IScenarioAi> AiStrats =>
        [new TopP2PartySynergyAi(), new TopP2PartySynergyLpduAi()];

新跑法實作 IScenarioAi<TopP2PartySynergyState>，提供 Name、可選的 Group，以及 Run(state, world)。Run 取得同一份隨機機制結果，依該結果決定機器人位置。Group 用於介面分類；不要將地區名稱當成會自動套用攻略的功能。

實際工作步驟：
1. 從最接近的 AI 複製成新類別與新檔案，保留原跑法。
2. 改 Name；如有必要設定 Group。
3. 將攻略逐步轉成「何時開始移動、誰去何處、最晚何時到位」。
4. 根據 State 的標記、Debuff、方向、分攤對象等結果分配角色。
5. 在 AiStrats 末尾加入新 AI，避免無必要地改動既有索引。
6. 跑多個隨機種子，再在遊戲內驗證玩家各位置、移動路徑、畫面與重開行為。

### 移動 API 的意義

LPDU 的現有例子：
    var ai = new AiManager(world);
    ai.Move(11f, AttackDodge, arrivalTime: 14.8f);
    ai.Move(16f, SpreadPositions, arrivalTime: 21.5f);

這代表排定事件，透過回呼取得當次目標站位，並以 arrivalTime 作為預期到位時間。AiManager 會依距離安排延後出發或 Sprint；它不是保證任何距離都能及時到位的瞬間傳送。

AiMove 的流程：
    AiMove.Create(八個站位)
          .Assignments(state.Order.List)
          .ApplySwaps(交換規則)
          .ApplyPositions(旋轉或其他位置調整);

若站位直接按角色順序排列，可用 NaturalOrder()。Vector2 的兩個分量對應場景局部 X、Z；AiManager 會轉為 Vector3(local.X, 0, local.Y)。不要直接混入遊戲全域座標。

角色分配與座標轉換是兩件事：ApplySwaps 改誰去哪個位置，ApplyPositions 改位置幾何。近遠線、同側分攤、新北方向等變化要使用 State，不能只寫死八個點。

jitter 會使位置有小幅隨機偏移；極窄安全區要特別驗證。Run 保存的狀態每次必須重新初始化。若採 Tick 動態反應，還須確認 Scenario 實際呼叫所選 AI 的 Tick；只實作介面方法不會自動被執行。

### 時間語意容易踩的坑

EventScheduler.Add(offset, action) 的實作是「目前 elapsed + offset」。
在機制起始一次排程時，數字可視為從起點算起；但在事件回呼內再 Add，數字會成為從當下開始的延遲。不要將回呼內 Add(20, ...) 誤認為固定在開場第 20 秒。
FromRunStart 提供特殊的起點排程上下文，需按現有使用方式理解。

### 程式邊界

依 AGENTS.md，跑法差異放在 *Ai；*Scenario 與 *State 描述所有跑法共同面對的機制。不要為了某個跑法方便而修改 Boss 傷害、判定時間或抽籤結果。機制隨機值使用既有 world.Rng / State；核心 jitter 使用 world.Stream("ai-jitter")。

## 4. 如果要新增「尚未存在的機制」

工作量比新增跑法大。通常需要：
- Zone / Phase：場地、原點、路標、天氣與音樂。
- *Constants.cs / *Actions.cs：遊戲 ID、EnemyAction 定義。
- *Scenario.cs：Boss 生成、讀條、事件時間軸、狀態與失敗處理。
- *State.cs：每次執行的隨機機制結果。
- *Ai.cs：至少一套可通過的機器人解法。
- *StateOverrides.cs / *SettingsWindow.cs：需要時提供固定抽籤與練習設定。
- ScenarioCatalog.Create：加入新 Scenario。
- 測試：涵蓋正常解法、隨機分支與應失敗的錯誤行為。

優先沿用既有 EnemyAction、形狀與遊戲資料，不要自行猜全部讀條和範圍。tools/parser.py 可從日誌產生基底，但隨機化、失敗條件與 AI 仍需人工補齊；不是把影片或攻略貼入就能完整生成。

工具檔頭提供的入口：
    python tools/parser.py <log_path> [<territory_id_decimal>]

多人功能另外檢查 SupportsMultiplayer、IMultiplayerReplayable 與狀態重播。不能僅把 SupportsMultiplayer 設為 true 就宣稱可用。AGENTS.md 也註明多人架構正在重寫，須以具體程式與測試結果為準。

## 5. 編譯與驗證條件

現場檢查：
- 插件專案：Dalamud.NET.Sdk/15.0.0，版本欄位 0.5.1.0。
- tests/AnoMech.Tests：net10.0-windows、x64。
- dotnet --list-sdks 目前僅列出 8.0.403。
- 預設 Dalamud.dll 路徑存在，但未驗證 API/遊戲版本相容性。

因此這次沒有編譯或執行測試，也沒有安裝額外 SDK。要執行測試，至少先補齊 .NET 10 SDK 與相容的 Dalamud 開發檔案。原始碼下載完成不能當成目前遊戲可執行的證據。

環境齊備後，可在儲存庫根目錄執行：
    dotnet build AnoMech.sln
    dotnet test tests/AnoMech.Tests/AnoMech.Tests.csproj --filter FullyQualifiedName~ScenarioCatalogTests

ScenarioCatalogTests 會列舉每個 Scenario 的 AiStrats。SunnyDay 的 SunnySeeds 預設值是 1，不是大量種子覆蓋；新增跑法後應透過 runsettings 增加 SunnySeeds，並保留失敗 seed 重播。測試建置還會下載固定版本的 ffxiv-datamining CSV。

邏輯測試通過之後，仍需遊戲內檢查特效、讀條、走位時間、各玩家角色與退出清理。多人需另外做 Host + Client 驗證。

## 6. 實作某套跑法前需要的資料

指定「副本／階段／機制」與「跑法名稱或攻略連結」，再整理八人職責、優先序、方向基準、交換條件及關鍵到位時間，即可對應到具體 AI 檔案。

本次只分析擴充方式；未選定攻略，也沒有新增假定的跑法。
