# 本機開發與建置

固定專案：`D:\FF14\ModCoding\AnoMech`。後續與朋友合作的 AnoMech 修改都在這裡進行，工作規則記錄於 `AGENTS.md`。

## 已合併的自訂內容

從 `D:\FF14\Mods\AnoMechMine` 移植 10 個既有檔案的差異及 16 個新增程式／測試／攻略檔案。兩份專案基底均為 `1646a7068c2071a3d1fd9e3c84550dfd3f1b6ade`，移植後逐檔核對內容一致，未覆蓋其他核心檔案。

- `(custom)` 名稱與 Yoruha 製作人。
- 可切換的 BGM 設定、P1 Yoruha TP 跑法。
- P2 Trine 地板預兆、左右翼及攻略。
- P3 完整第一次決戰、TLB3、麻將預兆與 Limit Cut 銜接。
- 兩王先拉到中央、第一組 MT 無敵兩連、第二組 MT → ST。
- 對應測試與歷史攻略／設計記錄；第三組雷與 Black Hole 不另改動。

## 建置命令

在專案根目錄執行：

```powershell
.\tools\Rebuild-Custom.ps1
```

預設先跑完整插件測試，再 Release Rebuild。需要針對自訂機制驗證時：

```powershell
.\tools\Rebuild-Custom.ps1 -TestFilter 'FullyQualifiedName~UmadP1HinoTests|FullyQualifiedName~UmadP2TrineTests|FullyQualifiedName~UmadP3FirstDecisiveBattleTests|FullyQualifiedName~LimitCut'
```

同一份程式已完成適當測試時，重建可使用 `-SkipTests`；`build-info.json` 會如實標示略過測試。

固定輸出：

- `D:\FF14\Mods\AnoMesh\AnoMech.dll`
- `D:\FF14\Mods\AnoMesh\AnoMech-custom-Yoruha.zip`

每次成功建置都取代上次輸出，刪除舊產物及短暫的 `.previous`，不新增日期備份或其他 mod 副本。測試／編譯失敗則保留上一份成功發布的套件。清理不包含原始碼、Git、遊戲設定、其他模組或朋友的開發目錄。

## 本次驗證與載入

2026-11-24：68 個自訂機制與 Limit Cut 專項通過。Release Rebuild 成功，保留既有 `UltimateSuppressionScenario.cs:403` 的 CS8602 警告。第二次以相同來源 Rebuild 驗證替換流程：舊檔測試標記已刪除，artifacts 只剩固定目錄及 ZIP，沒有暫存備份殘留；兩次 DLL SHA256 一致。

本次未重跑全部場景回歸，也未重新驗證遊戲內畫面／聲音／多人。

目前 Dalamud 設定仍指向另一份 `D:\FF14\Mods\Duty-combat-test\AnoMech.dll`，其中含朋友的 FRU 預覽檔案，因此沒有擅自覆蓋該版本。要載入本專案的合併版，請在 Dalamud 開發插件設定改選固定輸出的 `D:\FF14\Mods\AnoMesh\AnoMech.dll`；本次未直接修改運行中的遊戲設定。

發布位置已依使用者最新要求固定為 `D:\FF14\Mods\AnoMesh`；原始碼與開發仍固定在 `D:\FF14\ModCoding\AnoMech`。每次成功 Rebuild 替換這個資料夾內的舊產物。
