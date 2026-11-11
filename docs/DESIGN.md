# MyDrawVector 系統設計文件

- 文件版本：1.0
- 對應軟體版本：1.6.12
- 文件性質：設計觀點與決策紀錄
- 適用對象：維護者、貢獻者、功能規劃者

---

## 1. 目的與範圍

本文件說明 MyDrawVector 的設計理念、模組責任、資料結構、關鍵流程、演算法與設計決策。與 [系統架構與技術文件](ARCHITECTURE.md) 的差異在於：

- **ARCHITECTURE.md**：描述目前程式如何組成、類別與檔案的對應。
- **DESIGN.md**：描述為什麼這樣設計、流程如何運作、未來如何擴充。

範圍涵蓋向量文件模型、畫布渲染、工具互動、復原、檔案格式、資產管理、設定與發行。

---

## 2. 設計原則

1. **文件模型優先**：所有編輯都先修改 `Document` 模型，再更新視覺元素；不以 XAML 元素作為資料來源。
2. **核心可測試**：幾何、模型、序列化與歷史放在 `MyDrawVector.Core`，不依賴 UI。
3. **所見即所得**：畫布上的選取框、控制點與浮動工具列直接反映模型狀態。
4. **安全預設**：離線、最小權限、原子寫入、路徑檢查、不執行文件內容。
5. **可復原**：所有破壞性操作都可復原；刪除提供即時復原提示。
6. **適合教育情境**：圖示化工具列、清楚的回饋、避免過度複雜的設定。
7. **可攜式發行**：免安裝、可搬移、支援 x64 與 ARM64。

---

## 3. 使用案例

### 3.1 主要使用者

- 國小、國中、高中學生。
- 需要快速製作學習單、海報、卡片或課堂作品的老師。
- 需要離線、免安裝工具的一般使用者。

### 3.2 典型流程

```text
開啟程式
  → 新增或開啟文件
  → 選擇畫布尺寸與背景
  → 建立圖形／文字／圖片／筆畫
  → 調整樣式、位置、圖層
  → 復原／重做微調
  → 儲存 .mydraw
  → 匯出 PNG
  → 關閉程式
```

### 3.3 例外流程

- 開啟損壞檔案 → 顯示錯誤，不讓程式崩潰。
- 圖片資產遺失 → 顯示半透明佔位，文件仍可編輯。
- 程式異常結束 → 下次啟動詢問是否恢復自動儲存。
- 搬移程式資料夾 → 啟動時自動修復檔案關聯。
- 重複啟動 → 帶回既有視窗並開啟指定檔案。

---

## 4. 系統情境

```text
┌──────────────┐        ┌─────────────────────────────┐
│   使用者      │───────►│ MyDrawVector（WinUI 3）      │
└──────────────┘        │  ├─ 畫布與工具               │
                        │  ├─ 文件模型                 │
                        │  ├─ 復原歷史                 │
                        │  └─ 檔案／設定／資產          │
                        └──────────┬──────────────────┘
                                   │
             ┌─────────────────────┼─────────────────────┐
             ▼                     ▼                     ▼
      ┌────────────┐       ┌────────────┐       ┌────────────┐
      │ .mydraw     │       │ 圖片檔案    │       │ 使用者設定  │
      │ ZIP + JSON  │       │ PNG/JPEG…  │       │ JSON       │
      └────────────┘       └────────────┘       └────────────┘
```

系統不連線網路，也不與雲端服務互動。

---

## 5. 模組設計

### 5.1 文件模型模組（Core）

責任：

- 定義文件、圖層、物件與樣式。
- 提供變形、命中測試、邊界與排序。
- 提供 JSON 與 ZIP 序列化。
- 提供快照式復原歷史。

設計重點：

- 物件以 `SceneObject` 抽象，透過 `type` 判別多型。
- 圖層順序與物件 Z 序分離，`OrderedObjects()` 負責扁平化。
- `EnsureLayers()` 集中處理舊版遷移與資料正規化。

### 5.2 ViewModel 模組（App）

`EditorViewModel` 是 UI 與 Core 之間的協調者：

- 持有目前 `Document`、`CurrentFilePath`、選取集合與 `UndoManager`。
- 提供新增、刪除、再製、貼上、對齊、圖層操作。
- 透過 `DocumentChanged` 與 `SelectionChanged` 通知 UI。
- 維護 `IsDirty`、`ChangeVersion` 與狀態文字。

### 5.3 渲染模組（App）

責任：

- 將 `Document` 轉為 XAML 視覺元素。
- 依物件 ID 快取與增量更新。
- 套用檢視矩陣、頁面裁切與 overlay。
- 處理格線、棋盤格、頁面陰影與空文件提示。

設計重點：

- 物件視覺元素不參與命中測試，所有輸入由 `CanvasHost` 統一處理。
- Overlay 使用螢幕座標，控制點大小不隨縮放改變。
- `MatrixTransform` 每次重新指派，避免 WinUI 3 不即時重繪。

### 5.4 輸入與工具模組（App）

- `DragMode` 狀態機處理平移、移動、縮放、旋轉、框選、建立圖形與鉛筆。
- 工具列舉 `EditorTool` 決定目前工具。
- 手勢開始時保存文件快照，結束時建立單一復原紀錄。
- 控制點優先於物件命中；鎖定與隱藏物件不參與畫布選取。

### 5.5 服務模組（App）

| 服務 | 設計責任 |
| --- | --- |
| `SettingsService` | 延遲寫入、舊版遷移、最近檔案 |
| `AutosaveService` | 週期性快照、恢復中繼資料、刪除 |
| `FileService` | 將 `StoredImage` 轉為位元組後存開 |
| `FileAssociationService` | HKCU 關聯建立／移除／檢查 |
| `ImageAssetStore` | 雜湊去重、解碼、顯示快取 |
| `FileLoggerProvider` | 日誌檔與輪替 |

---

## 6. 資料設計

### 6.1 文件結構

```text
Document
├── Canvas: CanvasSettings
│   ├── Width
│   ├── Height
│   ├── Background
│   └── Preset
├── Layers: List<Layer>
│   ├── Id
│   ├── Name
│   ├── Order
│   ├── IsVisible
│   └── IsLocked
├── Objects: List<SceneObject>
│   ├── ShapeObject
│   ├── TextObject
│   ├── ImageObject
│   └── PathObject
└── Assets: List<AssetInfo>
```

### 6.2 物件共同欄位

| 欄位 | 型別 | 說明 |
| --- | --- | --- |
| `id` | string | GUID `"N"` 格式 |
| `name` | string | 顯示名稱 |
| `type` | string | `shape`、`text`、`image`、`path` |
| `layerId` | string | 所屬圖層 |
| `zIndex` | int | 圖層內順序 |
| `transform` | object | 中心、尺寸、旋轉、翻轉 |
| `opacity` | double | 0～1 |
| `isVisible` | bool | 是否顯示 |
| `isLocked` | bool | 是否鎖定 |

### 6.3 各物件欄位

#### ShapeObject

| 欄位 | 說明 |
| --- | --- |
| `kind` | `Rectangle`、`RoundedRectangle`、`Ellipse`、`Line`、`Arrow`、`Triangle`、`Diamond`、`Pentagon`、`Hexagon`、`Star`、`SpeechBubble` |
| `fill` | 填色色碼 |
| `stroke` | 邊框色碼 |
| `strokeWidth` | 邊框粗細 |
| `dash` | `solid`、`dash`、`dot` |
| `cornerRadius` | 圓角半徑 |
| `isFilled` | 是否啟用填色 |
| `isStroked` | 是否啟用邊框 |

#### TextObject

| 欄位 | 說明 |
| --- | --- |
| `text` | 文字內容 |
| `fontFamily` | 字型家族 |
| `fontSize` | 字級 |
| `fontWeight` | 0 表示沿用舊版 `bold` 設定 |
| `bold`、`italic`、`underline` | 文字樣式 |
| `color` | 文字顏色 |
| `align` | `Left`、`Center`、`Right` |
| `lineSpacing` | 行距倍率 |

#### ImageObject

| 欄位 | 說明 |
| --- | --- |
| `assetId` | 對應 `AssetInfo.Id` |
| `stretch` | 圖片顯示方式（目前渲染固定使用 Fill） |

#### PathObject

| 欄位 | 說明 |
| --- | --- |
| `points` | 0～1 正規化區域座標點集合 |
| `stroke` | 線條顏色 |
| `strokeWidth` | 線條粗細 |
| `dash` | 線條樣式 |

### 6.4 設定結構

```text
AppSettings
├── 視窗位置與大小
├── ShowGrid / SnapToGrid / AutoSelectToolOnObjectClick
├── ThemeMode / ThemeColor
├── ShapeToolStyles: Dictionary<ShapeKind, ShapeToolStyle>
├── TextStyle: TextToolStyle
├── PencilStroke / PencilStrokeWidth / PencilDash
└── RecentFiles
```

設定儲存在 `%AppData%\MyDrawVector\settings.json`，採暫存檔加取代方式寫入。

---

## 7. 關鍵流程設計

### 7.1 啟動流程

```text
Main
 → Application.Start
 → App.OnLaunched
 → AppInstance.FindOrRegisterForKey
    ├─ 非主要實例 → 轉交啟用參數 → Exit
    └─ 主要實例
        → 初始化日誌與 DI
        → 處理 --register / --unregister
        → 建立 MainWindow
        → RootGrid_Loaded
            → ApplyTheme / RenderAll
            → FitToPage
            → 啟動自動儲存計時器
            → 嘗試恢復自動儲存
            → 開啟啟動檔案
```

### 7.2 開啟文件

```text
使用者選擇檔案
 → DocumentPackageService.OpenAsync
    → 檢查 document.json
    → 反序列化 Document
    → Document.EnsureLayers()
    → 讀取 assets/*
 → ImageAssetStore.LoadAsync
 → EditorViewModel.SetDocument
 → MainWindow.RenderAll
 → 更新最近檔案與標題
```

### 7.3 儲存文件

```text
使用者按儲存
 → 取得目前路徑或開啟另存對話框
 → 補上 .mydraw 副檔名
 → FileService.SaveAsync
    → DocumentPackageService.SaveAsync
       → 寫入 document.json
       → 寫入 manifest.json
       → 寫入 assets/*
       → 暫存檔 + 原子取代
 → 清除 IsDirty
 → 刪除自動儲存
 → 更新最近檔案與狀態列
```

### 7.4 建立圖形

```text
選擇圖形工具
 → PointerPressed：開始 Shape 手勢
 → PointerMoved：更新預覽幾何
 → PointerReleased
    → 計算邊界、套用吸附
    → 建立 ShapeObject
    → 套用該工具記憶樣式
    → PushUndo()
    → AddObject()
    → RenderAll()
```

### 7.5 選取與變形

```text
PointerPressed
 → 檢查控制點／旋轉手柄
 → 否則命中物件
    → 更新選取
    → 保存手勢前快照
 → PointerMoved
    → Move／Resize／Rotate
    → 套用格線吸附與 Shift 約束
 → PointerReleased
    → 推入復原紀錄
    → MarkDirty()
```

### 7.6 復原與重做

```text
Undo
 → 記住目前選取 ID
 → History.Undo(目前快照)
 → 反序列化上一個快照
 → 還原選取
 → ChangeVersion++
 → DocumentChanged
 → RenderAll
```

### 7.7 自動儲存與恢復

```text
每 60 秒
 → 文件 dirty 且 ChangeVersion 有變
 → AutosaveService.SaveAsync
 → 更新狀態列

下次啟動
 → AutosaveService.HasRecoverable
 → 詢問恢復／捨棄／稍後
```

### 7.8 匯出 PNG

```text
選擇倍率與透明背景
 → 暫時調整場景檢視與背景
 → RenderTargetBitmap
 → 記憶體編碼 PNG
 → File.WriteAllBytesAsync
 → 還原場景狀態
```

---

## 8. 演算法設計

### 8.1 物件變形

```text
M = T(-W/2, -H/2) × S(flipH, flipV) × R(rotation) × T(center)
```

- 物件以中心為原點。
- 翻轉以區域軸 scale 正負號表示。
- 世界邊界取四個角落的外接矩形。

### 8.2 縮放

1. 將滑鼠世界座標轉到物件區域座標。
2. 依控制點決定固定對角與拖曳方向。
3. 計算新寬高，必要時鎖定比例。
4. 套用最小尺寸 8 DIP。
5. 依旋轉後軸向量換算中心位移。
6. 套用格線吸附。

### 8.3 旋轉

1. 以物件中心計算滑鼠角度。
2. 減去起始角度。
3. `Shift` 時吸附到 15° 倍數。
4. 正規化到 0～360。

### 8.4 命中測試

- 先檢查物件與圖層可見性、鎖定狀態。
- 世界座標轉區域座標。
- 依物件類型使用矩形、橢圓或線段距離。
- 圖層由高到低、物件由高 Z 到低 Z。

### 8.5 格線吸附

```text
snapped = round(value / 20) * 20
if |value - snapped| <= 8 → 使用 snapped
```

### 8.6 圖片去重

```text
hash = SHA256(bytes)
id = "img_" + hex(hash)[0..16]
if assets 已存在 id → 重用
```

### 8.7 復原歷史

- 每次操作保存精簡 JSON 快照。
- 合併時間窗 700 ms。
- 上限 100 筆。
- 新編輯清空 redo。

---

## 9. 狀態機設計

### 9.1 工具狀態

```text
EditorTool = Select | Rectangle | RoundedRectangle | Ellipse | Line |
             Arrow | Triangle | Diamond | Pentagon | Hexagon | Star |
             SpeechBubble | Text | Image | Pencil | Pan
```

### 9.2 拖曳狀態

```text
None ──► Pan
None ──► Move
None ──► Resize
None ──► Rotate
None ──► Marquee
None ──► Shape
None ──► Pencil
任何狀態 ──► None（放開、取消或捕捉遺失）
```

狀態轉移由 `CanvasHost` 的 Pointer 事件控制，並以 `_dragMode` 保存目前狀態。

---

## 10. 檔案格式設計

### 10.1 容器

```text
manifest.json
document.json
assets/
```

### 10.2 manifest.json

| 欄位 | 說明 |
| --- | --- |
| `schema` | 固定 `mydrawvector` |
| `version` | 容器版本，目前為 2 |
| `appVersion` | 產生檔案的應用程式版本 |
| `created` | 建立時間 |
| `modified` | 最後修改時間 |

### 10.3 document.json 範例

```json
{
  "canvas": {
    "width": 1123,
    "height": 794,
    "background": "#FFFFFFFF",
    "preset": "A4-landscape"
  },
  "layers": [
    {
      "id": "a1b2c3",
      "name": "圖層 1",
      "order": 0,
      "isVisible": true,
      "isLocked": false
    }
  ],
  "objects": [
    {
      "type": "shape",
      "id": "o1",
      "name": "矩形 1",
      "layerId": "a1b2c3",
      "zIndex": 0,
      "transform": {
        "centerX": 300,
        "centerY": 200,
        "width": 240,
        "height": 120,
        "rotation": 15,
        "flipH": false,
        "flipV": false
      },
      "opacity": 1,
      "isVisible": true,
      "isLocked": false,
      "kind": "Rectangle",
      "fill": "#FF4F9CFF",
      "stroke": "#FF1F2937",
      "strokeWidth": 2,
      "dash": "solid",
      "cornerRadius": 12,
      "isFilled": true,
      "isStroked": true
    }
  ],
  "assets": []
}
```

### 10.4 相容性策略

- 容器版本供未來遷移使用。
- 舊版沒有 `layers` 時自動建立預設圖層。
- 缺少欄位時使用模型預設值。
- 目前不保留未知欄位；未來可加入 `JsonExtensionData` 提升向前相容性。

### 10.5 安全設計

- 資產檔名必須是單純檔名，不可包含路徑或 `..`。
- 缺少資產時跳過，不讓整份文件失敗。
- 儲存採原子寫入，降低中途失敗造成檔案損毀的風險。

---

## 11. 錯誤處理與復原策略

| 情境 | 處理 |
| --- | --- |
| 開啟損壞檔案 | 顯示錯誤訊息，保留目前文件 |
| 缺少圖片資產 | 顯示半透明佔位，允許替換或復原 |
| 儲存失敗 | 顯示錯誤，文件維持 dirty |
| 自動儲存失敗 | 寫入日誌 warning，不中斷編輯 |
| 未處理例外 | 寫入日誌，顯示友善訊息 |
| 拖曳中斷 | 取消互動並還原手勢前快照 |
| 設定檔損壞 | 使用預設設定 |
| 檔案關聯失敗 | 記錄 warning，不影響編輯 |

---

## 12. 設計決策與取捨

| 決策 | 選擇 | 理由 | 代價 |
| --- | --- | --- | --- |
| 原生格式 | 自訂 ZIP + JSON | 可保存圖層、翻轉、資產與未來擴充 | 需自行維護格式與遷移 |
| 渲染 | XAML 場景圖 | 開發快、文字與 IME 支援佳、無額外繪圖引擎 | 大量物件時效能有限 |
| 變形 | `Matrix3x2` + `MatrixTransform` | 與 XAML 整合直接 | WinUI 重繪行為需特別處理 |
| 復原 | JSON 快照 | 實作簡單、涵蓋所有變更 | 大型文件記憶體與序列化成本較高 |
| 合併 | 時間窗 + coalesce key | 連續調整只留一筆 | 不同物件同屬性可能被合併 |
| 圖片 | 內容雜湊去重 | 減少檔案大小 | 需保存原始位元組 |
| 檔案關聯 | HKCU | 免管理員權限 | 搬移資料夾後需修復 |
| 發行 | 非 self-contained | 檔案較小、可共用執行環境 | 使用者需安裝 Runtime |
| 架構 | Core 與 UI 分離 | 可測試、可演進 | 部分流程仍需 code-behind 協調 |
| 平台 | x64 + ARM64 | 涵蓋一般 PC 與 ARM 裝置 | 需維護兩份發行與測試 |

---

## 13. 擴充點

### 13.1 新增圖形

1. 在 `ShapeKind` 加入新種類。
2. 在 `BuildShapeGeometry` 加入幾何。
3. 在工具列加入按鈕與資源字串。
4. 在 `ContainsLocal` 加入命中測試（若不同於矩形）。
5. 加入序列化往返測試。

### 13.2 新增物件類型

1. 繼承 `SceneObject`。
2. 加入 `JsonDerivedType` 與 `type` 名稱。
3. 在 `RenderAll` 建立對應視覺元素。
4. 加入命中測試、Clone、屬性面板區塊與測試。

### 13.3 新增匯出格式

1. 建立對應的匯出服務。
2. 從 `Document` 產生目標格式。
3. 加入檔案選單與對話框。
4. 加入輸出測試或手動驗收清單。

### 13.4 新增主題

1. 在 `AppThemePalette` 加入顏色與資源鍵。
2. 在 `Resources.resw` 加入名稱。
3. 確認淺色、深色與高對比可讀性。
4. 加入設定遷移與回退測試。

---

## 14. 測試與驗收

### 14.1 自動化測試

- Core 單元測試：幾何、圖層、序列化、ZIP、復原。
- 建置與格式驗證：`dotnet build`、`dotnet test`、`dotnet format --verify-no-changes`。

### 14.2 手動驗收

- 啟動與關閉流程。
- 檔案關聯與單一實例。
- 建立、選取、變形、對齊、圖層。
- 復原／重做與合併。
- 自動儲存與恢復。
- PNG 匯出與透明背景。
- x64 與 ARM64 發行。
- DPI、深色模式、高對比、中文 IME。

### 14.3 品質門檻

- 建置 0 警告、0 錯誤。
- 單元測試全數通過。
- 格式驗證無差異。
- 主要操作不應造成崩潰或資料遺失。

---

## 15. 風險與未來方向

| 風險 | 影響 | 對策 |
| --- | --- | --- |
| 大型文件效能 | 縮放與屬性調整卡頓 | 增量更新、快取、必要時改用 Win2D |
| 快照式復原記憶體 | 大型文件佔用較多記憶體 | 只保存差異或改用命令模式 |
| 檔案格式演進 | 舊檔相容性 | 容器版本、遷移測試、保留未知欄位 |
| 缺少實機測試 | 特定 DPI／ARM64 問題 | 建立實機測試清單與 CI |
| Runtime 相依 | 使用者無法啟動 | 文件說明、安裝檢查、友善錯誤訊息 |
| 高對比與無障礙 | 部分使用者無法操作 | Narrator 測試、焦點順序與對比檢查 |

未來優先項目：

1. 群組／解群組與智慧參考線。
2. SVG／JPG／PDF 匯出與列印。
3. 範本庫與均分。
4. 圖片裁切與更多圖片選項。
5. 觸控手勢與觸控筆壓感。
6. 效能優化與更細緻的增量渲染。
7. 完整 ARM64 與 Windows 10 1809 實機驗證。

---

## 16. 相關文件

- [使用指南](USER_GUIDE.md)
- [系統架構與技術文件](ARCHITECTURE.md)
- [功能規格](FEATURE_SPEC.md)
- [開發與發行指南](DEVELOPMENT.md)
- [開發紀錄](DEV_LOG.md)
- [版本異動](../CHANGELOG.md)
- [授權](../LICENSE)
