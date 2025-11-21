# Counter-Strike 2 Server Plugin

一個使用 .NET 8 開發的 Counter-Strike 2 伺服器插件專案，基於 [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) 框架打造。

> **注意**：本專案為個人實驗性專案，並不包含完整的設定檔案（如 `server.cfg`、`botprofile.db`、`sounds.json` 等）。

## 🎯 專案特色

### 核心架構
- **框架**：CounterStrikeSharp + .NET 8
- **資料庫**：Entity Framework Core + MS SQL Server
- **架構模式**：Interface/Service 分層設計
- **快取機制**：玩家資料首次載入後使用記憶體快取，減少資料庫 I/O

### 效能優化
- **非同步處理**：使用 `async/await` 處理 Bot 行為與資料庫操作
- **並發控制**：對遊戲事件與任務進行適當的延遲與併發處理
- **ConVar 快取**：避免重複查詢遊戲變數，提升執行效率

## 🎮 核心模組

### 1. Bot 管理模組 (IBot)
負責遊戲中 Bot 的生命週期與行為控制。

**主要功能：**
- **地圖生命週期**：MapStart、WarmupEnd 行為
- **回合控制**：RoundStart、RoundEnd 行為處理
- **動態難度**：根據連勝/連敗調整 Bot 難度等級
- **Bot 重生系統**：支援 Bot 死亡後自動重生
- **特殊 Bot**：Boss、Special Bot 管理

**核心方法：**
```csharp
Task MapStartBehavior(string mapName);
Task WarmupEndBehavior(string mapName);
Task RoundStartBehavior(string mapName);
Task RoundEndBehavior(int winStreak, int loseStreak, string mapName);
Task RespawnBotAsync(CCSPlayerController bot, string mapName);
void BossBehavior(CCSPlayerController boss);
```

### 2. 音樂系統模組 (IMusic)
提供完整的遊戲音效管理，支援回合音樂、勝負音樂與終局音樂。

**主要功能：**
- **熱身音樂**：玩家加入時播放
- **回合音樂**：每回合隨機播放，支援停止控制
- **勝負音樂**：根據回合結果播放對應音樂
- **終局音樂**：遊戲結束時播放
- **音量控制**：依玩家個人音量設定調整

**技術實作：**
- 使用 UserMessage ID 209 精確停止特定音效
- 追蹤回合音樂的 Sound Event Index
- 分離不同類型音效的播放邏輯

**核心方法：**
```csharp
void PlayWarmupMusic(CCSPlayerController player);
void PlayRoundMusic();
void StopRoundMusic();
void PlayRoundWinMusic();
void PlayRoundLoseMusic();
void PlayEndGameMusic();
```

### 3. 指令管理模組 (ICommand)
處理玩家與管理員的遊戲內指令。

**主要功能：**
- **玩家管理**：kick、slay、slap
- **伺服器控制**：地圖切換、Cvar 修改
- **玩家工具**：復活、模型選擇、音量調整
- **資訊查詢**：伺服器資訊、玩家列表

**指令列表：**
- `css_kick` - 踢出玩家
- `css_map` / `css_maps` - 切換地圖
- `css_cvar` - 修改伺服器變數
- `css_players` - 顯示玩家列表
- `css_slay` - 擊殺玩家
- `css_god` - 無敵模式
- `css_revive` - 復活玩家
- `css_models` - 模型選擇
- `css_slap` - Slap 玩家
- `css_volume` - 音樂音量設定

## 🛠️ 工具類別

### Utility Class
提供全域工具方法與資源管理。

**主要功能：**
- **音效管理**：從 `sounds.json` 載入音效事件
- **模型管理**：Workshop 模型載入與設定
- **地圖管理**：實體地圖與 Workshop 地圖列表
- **玩家工具**：
  - `DrawBeaconOnPlayer()` - 玩家標記光圈
  - `SlapPlayer()` - Slap 效果
  - `ColorScreen()` - 螢幕淡入淡出效果
  - `SetClientModel()` - 玩家模型設定
- **驗證方法**：人類玩家與 Bot 驗證

## 📦 資料模型

### 主要模型
- **Music** / **SoundEvent**：音效事件與顯示名稱
- **SkinInfo**：Workshop 模型資訊
- **Position**：玩家位置、旋轉、速度
- **WeaponStatus**：武器追蹤狀態

## 🎵 音效系統設計

### 設定檔格式 (sounds.json)
```json
{
    "Warmup": ["warmup.01", "warmup.02"],
    "Round": [
        {
            "EventName": "round.01",
            "DisplayName": "Music Title"
        }
    ],
    "EndGame": ["end.01"],
    "Loose": ["loose.01"],
    "Win": ["win.01"]
}
```

### 音效播放流程
1. **Warmup**：玩家加入隊伍時觸發
2. **Round Music**：每回合開始後 freezetime 結束時播放
3. **Round End**：自動停止回合音樂
4. **Win/Lose**：根據勝負播放對應音樂
5. **End Game**：最後一回合播放終局音樂

## 🚀 技術亮點

### 1. 音效精確控制
使用 CS2 網絡協議的 UserMessage ID 209 來精確停止特定音效實例，避免音樂重疊。

### 2. 快取優化
- 玩家資料快取減少資料庫查詢
- ConVar 查詢結果快取避免重複查詢
- Workshop 資源在地圖啟動時預載

### 3. 非同步設計
所有資料庫操作與 Bot 行為處理使用非同步模式，避免阻塞主執行緒。

### 4. 模組化架構
清晰的 Interface/Implementation 分離，便於測試與擴展。

## 📁 專案結構

```
MyProject/
├── Classes/
│   └── Utility.cs              # 工具類別
├── Models/
│   ├── Music.cs                # 音效模型
│   ├── Position.cs             # 位置資訊
│   └── WeaponStatus.cs         # 武器狀態
├── Modules/
│   ├── Interfaces/
│   │   ├── IBot.cs             # Bot 介面
│   │   ├── IMusic.cs           # 音樂介面
│   │   └── ICommand.cs         # 指令介面
│   ├── Bot.cs                  # Bot 實作
│   ├── Music.cs                # 音樂實作
│   └── Command.cs              # 指令實作
├── Services/
│   └── Interfaces/
│       ├── IPlayerService.cs   # 玩家服務介面
│       └── IPlayerManagementService.cs
└── Main.cs                     # 主插件類別
```

## 🔧 相依套件

- CounterStrikeSharp API
- Entity Framework Core
- Microsoft SQL Server Provider
- System.Text.Json

## 📝 待辦事項

- [ ] 發展 Coop/PvE 遊戲模式
- [ ] 實作 Boss 戰鬥系統
- [ ] 解決 GitHub Issues
- [ ] 完善設定檔案文檔
- [ ] 增加更多玩家互動功能

## 📄 授權

本專案僅供學習與研究使用。

## 🔗 相關連結

- **GitHub Repository**: https://github.com/raytzou/CounterStrikeSharpProject
- **CounterStrikeSharp**: https://github.com/roflmuffin/CounterStrikeSharp

---

*最後更新：Music Module 完成 - 支援完整的遊戲音效系統*
