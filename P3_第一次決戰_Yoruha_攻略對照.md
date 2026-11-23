# P3 第一次決戰：Yoruha TLB3 攻略對照
日期：2026-11-04
攻略：https://yan-flash.com/ultimate/yosei-ranbu#ult-phase-3
資料核對：https://github.com/awgil/ffxiv_bossmod/tree/master/BossMod.Ultimate/Dawntrail/Ultimate/UMAD
新增選單：Dancing Mad → P3 First Decisive Battle → JP → Yoruha
製作人：Yoruha

| 攻略重點 | 模擬器實作 |
|---|---|
| 第一次決戰：ST、H2、D1、D2 靠 Chaos；MT、H1、D3、D4 靠 Exdeath | 起始 AI 依指定分組，決戰依距離選四人付予 Epic/Fated 狀態 |
| 火、水、風水晶以數字方向出現 | 四種風方位＋火水鏡射，設定可固定或隨機 |
| 火水持有者為坦補兩名、DPS 兩名 | 各組抽取一名火、一名水；風向每人隨機 |
| TLB3 式依風、水站固定位置，火水交換時反轉基準 | 所有站位同步旋轉／鏡射；兩輪元素結算、水晶各選最近兩人 |
| MT 處理強化雷圈及之後二連強攻擊 | 雷圈判定、二連選最近目標；MT 需自行無敵或提供足夠減傷，NPC 沿用既有減傷簡化 |
| 斜向引導 Chaos 並躲兩輪縱橫爆破 | Chaos 預設斜向；兩種順序的扇形判定與 AI 避讓 |
| D3 遠距離誘導 Umbra Smash | 使用最遠玩家位置鎖定距離衰減，D3 離開人群再返回 |
| 正確面向解除風 | 沿用真空波正／反／側面判定與 10／40／20y 擊退 |
| 真空波約七成讀條使用坦 LB3，八人集合處理旋風 | 準備三格 LB；非坦玩家時 NPC 坦克自動 LB，坦玩家自行按 LB；旋風檢查人數與減傷 |
| 水晶主目標不因此解除風 | 主目標排除，僅實際清除的風產生後續旋風 |

驗證：已測試 100 個種子、32 種方位／鏡射／先元素／爆破順序組合與場內邊界，並含雷圈、爆破、錯誤誘導、錯面向與缺 LB 的負例。完整回歸結果另見 tests/AnoMech.Tests/TestResults/custom-yoruha-p3-final.trx。

限制：這是新加入的練習場景，時間軸依參考間隔編排，尚無本機實戰封包驗證；水晶模型、特效與實際視覺時機仍待遊戲內確認。HP、治療、護盾與累積傷害沿用模擬器簡化模型，未模擬完整補血循環。Boss 站位預設為攻略斜向，以練習站位／讀條／面向為主。
新增項目在真空波及最後旋風後結束；後續數字突進可選原有 P3 Limit Cut。
新增項目不啟用多人，待另外完成狀態同步與 Host + Client 驗證。
