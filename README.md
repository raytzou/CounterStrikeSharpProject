### 這是我個人 Counter-Strike 2 實驗性專案

也沒什麼好介紹的，我甚至連 config 都沒放上來，如： `server.cfg` 或 `botprofile.db`\
如果你建置完，可以順跑這個插件，你也是滿厲害的 👍

**以下是 ChatGPT 幫我產的介紹**

### CounterStrike Sharp Plugin (使用 .NET 8 開發)
GitHub Repository https://github.com/raytzou/CounterStrikeSharpProject

一款針對《Counter-Strike 2》所開發的遊戲伺服器插件，專案目標為強化伺服器玩家管理、自動化 BOT 控制，以及提升遊戲體驗。

## 系統架構與資料儲存：
- 使用 .NET 8 搭配 Entity Framework Core 與自架 MS SQL Server 進行玩家資料儲存。
- 簡單 Interface/Service 架構
- 快取機制，玩家首次進入時從資料庫載入資料，後續改用記憶體快取操作，大幅減少資料庫連線頻率與 I/O 負擔。

## 非同步流程與效能優化：
- 在 BOT 回合流程中使用 async/await 與非同步 API。
- 對遊戲事件與任務處理進行併發控制與延遲處理，提升伺服器效能與遊戲流暢度。

## 模組設計：
分為「玩家管理」、「BOT 控制」、「伺服器指令」三大模組，模組之間具清晰責任分離與介面設計。

## 未來 To Do
- 思考遊戲主題，發展成 Coop/PvE，打 BOSS
- 不知道，還在想
