# P3 第一次決戰 — Yoruha / TLB3 修正對照

## 本次補齊
- 開場兩秒準備：兩王立即出現；第一次「決戦」讀條在 t=2.2 開始，t=5.2 分組判定。ST/H2/D1/D2 去西側カオス，MT/H1/D3/D4 去東側エクスデス。
- 王的初始出生位置固定東西，水晶獨立隨機四角與炎水鏡射，不再把王與水晶整組旋轉。
- 保留炎／水先後、兩種 Implosion 順序、每種元素各一名 TH 與一名 DPS 的隨機指派。
- 混沌之風改為全員抽同一種正面／背面，不再八人各自抽。增加風向指定與 Reset all to random。
- 64 組幾何／順序／全員風向組合都納入專項測試；另外測試 100 個種子通關及 1000 種子隨機覆蓋。

## サンダガ辨識
強化版：47890，7 秒讀條，王自身 14.8y 大圓。本次增加原生 Channeling 64 的吸收黑線，由エクスデス連到場外的隱形輔助錨點。
普通版：47881，5 秒讀條，沒有吸收黑線；47884 在兩次命中各重新選最近玩家，5y 範圍死刑並附雷耐性下降。未改成全場攻擊。
錨點是為重現線條建立的本機 helper；精確外圈端點、伸手動畫與線條可見性需實機確認，不能由封包指令測試宣稱畫面通過。

## 誘導時段（下列時間包含開頭兩秒）
- t=5.3 分組後：カオス追 ST，エクスデス追 MT。真人坦克可移動拖王；讀條及原生動作鎖期間暫停追擊。
- 第一次水晶前：ST 把カオス拉向風側，MT 把エクスデス拉往風的對角。大圓雷 t=36.9 開始讀條後鎖位置，可離開避傷。
- t=48.4～56.9：エクスデス停止追擊，完成兩次最近人死刑；之後恢復追 MT，可拉向カオス。
- t=61.3～69.6：カオス停止追擊，兩次扇形共用已誘導好的位置與朝向。AI 依實際王的位置、朝向尋找安全扇區；遠程先在首次水晶後回中。
- 後半 MT 將エクスデス拉向風水晶；真空波讀條時鎖位。D3 誘導アンブラスマッシュ，讀條起點決定落點；看見讀條後回人群。

這些窗口根據讀條／命中時間與攻略誘導說明建立；沒有伺服器 AI 原始碼或這次實機移動封包，不能宣稱已逐幀重現追擊速度與每個解鎖時間。真人坦克的誘導結果會改變傷害起點，不再強制把王搬到攻略座標。
初始西／東的精確座標與拉王速度亦為模擬器近似值，尚待遊戲內核對。

## 驗證
本次 25 個 P3 專項通過。新增兩王被真人坦克拖動、強化雷讀條不被拖走、普通雷兩連與兩次扇形鎖位鎖朝向、黑線指令、開場無讀條的回歸。
測試檔：tests/AnoMech.Tests/TestResults/p3-fidelity.trx。
完整回歸另見 custom-yoruha-p3-fidelity-full.trx；最終數目記錄於發佈包「驗證結果.txt」。
P2 Trine 本次沒有修改；使用者已確認其遊戲內行為。

## 來源
- 使用者指定 TLB3：https://yan-flash.com/ultimate/yosei-ranbu#ult-phase-3
- P3 實戰機制說明：https://note.com/suri_ko/n/n1fce14ae7bba
- 技能與黑線 ID：https://github.com/awgil/ffxiv_bossmod/blob/master/BossMod.Ultimate/Dawntrail/Ultimate/UMAD/UMADEnums.cs
- 時間線：https://github.com/awgil/ffxiv_bossmod/blob/master/BossMod.Ultimate/Dawntrail/Ultimate/UMAD/UMADStates.cs
- 本機 SqPack Action 表：47890=7s/11+hitbox，47881=5s，47884=circle5；工具輸出 D:/FF14/ModCoding/AnoMech-reference/p3-thunder-probe.txt。
