# 門派資料格式

五份 JSON 檔以 UTF-8 儲存。每份最外層皆為物件。ID 需在同一份檔案中唯一，跨檔引用區分大小寫。資源、年份、月份與年齡使用整數，座標與尺寸可用小數。

- `buildings.json` 的 `buildings` 陣列包含 `id`、`name`、`description`、`color`（`#RRGGBB`）、`size`（依序為寬、高、深）、`cost`、`upkeep`（每月銀兩）、`income`、`unique`、`startBuilt`、`actions`（指令 ID 陣列）。唯一建築只能有一棟。開局先依 `start.json` 擺放，再將尚未擺放的開局建築依序填入空地。
- `actions.json` 的 `actions` 陣列包含 `id`、`name`、`description`、`type`、`cost`、`effect`。類型為 `generic`、`recruit` 或 `build`。招收另計 `recruitCost`。建造指令的成本、效果和建築成本在選定空地後一併結算。左側使用第一個建造指令和第一個招收指令。
- `start.json` 包含 `sectName`、`year`、`month`（1 至 12）、`silver`、`food`、`reputation`、`recruitCost`、`foodPerDisciple`（每人每月至少 1）與 `slots`。每個空地有 `x`、`z`（院中座標，–19 至 19）及可省略的 `buildingId`。空地座標不能重複。請為建物、入口和通道預留間距。
- `disciples.json` 包含 `disciples` 與 `generalLines`（通用台詞陣列）。每名弟子有 `id`、`name`、`gender`、`age`、`title`、`trait`、`robeColor`（`#RRGGBB`）、`startInSect`、`lines`（個人台詞陣列）。

`cost` 包含非負整數 `silver`、`food`。`income` 和 `effect` 包含整數 `silver`、`food`、`reputation`。沒有變動時填 0。

目前每棟提供 `lodge` 指令的建築計入四個床位，會顯示於「安頓弟子」結果與門派摘要。

- `tutorial.json` 包含 `intro`（含 `speaker`、`text` 的對話陣列）、`choosePrompt`、`titles`、`steps`、`completeText`、`reward`。每個步驟有 `id`、`title`、`objective`、`hint`、`trigger`、`target`、`highlight`。可在文字中使用 `{title}` 與 `{sect}`。
- 引導事件為 `camera_move`、`camera_zoom`、`click_building`（以 `target` 指定建築 ID）、`talk_disciple`、`build_any`、`recruit`、`end_month`、`read_report`。提示可留空，或以 `building:<id>` 或 `ui:<key>` 指定。支援的介面鍵為 `cmd_build`、`cmd_recruit`、`end_month`。
`actions.json` 的每個指令可設非負整數 `apCost`（預設 0），成功執行時才扣除。建造在選定空地後結算。`ap.json` 的 `baseMax` 是每月補滿的行動力基礎上限，預設 4。
