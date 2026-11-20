# wuxia-sect

## 里程碑 0｜武俠宗門經營

唐玄宗開元元年，師父下山未歸，由你這位大師兄／大師姐代掌崤山劍門。以俯視視角巡看院落、建造屋舍、招收弟子，按月結算門派收入、維護費及口糧。

1. 在 Unity Hub 選 **Projects → Add → Add project from disk**。
2. 選取 `C:\Dev\wuxia-sect`，用 **Unity 6000.6.10f1** 開啟。
3. 按編輯器上方的 **Play（▶）**。任何場景都會在進入 Play 模式後生成院落。若想建立專案的主場景，可在編輯模式選 **Wuxia → Create Main Scene** 一次。

### 操作

- **WASD／方向鍵**移動視角。按住**右鍵／中鍵**拖曳，使用**滾輪**縮放。
- **左鍵**點建築查看資料與指令，或點弟子交談。點對話框外即可收起對話。
- 從左側選**建造**，選好建築後點綠色空地。按 **Esc／右鍵**取消建造。
- 從左側或大殿選**招收弟子**。按右上方的**結束本月**查看月報並推進日期。

### 資料夾

- `Assets/Scripts/Core` 管理啟動、指令、資源與月份。
- `Assets/Scripts/Data` 定義與讀取 JSON 資料。
- `Assets/Scripts/World` 建立院落、建築、弟子與視角。
- `Assets/Scripts/UI` 建立操作介面、月報、對話及名稱標籤。
- `Assets/StreamingAssets/Data` 存放開局、建築、指令與弟子的資料，欄位見 [SCHEMA.md](Assets/StreamingAssets/Data/SCHEMA.md)。
- `Assets/Editor` 提供主場景選單。
