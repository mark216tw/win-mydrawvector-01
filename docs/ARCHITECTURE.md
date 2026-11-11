# MyDrawVector 系統架構與技術文件

- 文件版本：1.0
- 對應軟體版本：1.6.12
- 文件性質：現況架構說明（As-Is Architecture）
- 適用對象：維護者、貢獻者、技術審查者

---

## 1. 系統概觀

MyDrawVector 是一套 Windows 桌面向量繪圖應用程式，使用 WinUI 3、Windows App SDK 與 .NET 10 建置。系統以「文件模型」為核心，將編輯狀態與 UI 分離，並以 XAML 場景圖呈現畫布內容。

```text
┌─────────────────────────────────────────────────────────────┐
│                        MyDrawVector.exe                      │
│  ┌──────────────────────┐      ┌──────────────────────────┐  │
│  │  Presentation Layer  │      │   Application Services   │  │
│  │  MainWindow (XAML)   │◄────►│  EditorViewModel         │  │
│  │  Controls / Helpers  │      │  Settings / Autosave     │  │
│  └──────────┬───────────┘      │  File / Association      │  │
│             │                  │  ImageAssetStore         │  │
│             ▼                  └────────────┬─────────────┘  │
│  ┌──────────────────────┐                   │                │
│  │   Rendering / Input  │                   │                │
│  │  SceneGraph + Matrix │                   │                │
│  └──────────┬───────────┘                   │                │
└─────────────┼───────────────────────────────┼────────────────┘
              │                               │
              ▼                               ▼
┌─────────────────────────────────────────────────────────────┐
│                     MyDrawVector.Core.dll                    │
│  Models / Geometry / History / Serialization                │
│  Document、SceneObject、ObjectTransform、UndoManager          │
│  DocumentSerializer、DocumentPackageService                  │
└─────────────────────────────────────────────────────────────┘
              │
              ▼
        .mydraw ZIP（manifest.json / document.json / assets）
```

### 1.1 設計目標

- **離線優先**：不依賴網路、帳號或雲端服務。
- **可攜式發行**：免安裝資料夾，支援 x64 與 ARM64。
- **教育情境**：操作直覺、圖示化工具列、清楚的復原與自動儲存。
- **可測試核心**：文件模型、幾何與序列化不依賴 UI，可 headless 測試。
- **可演進格式**：`.mydraw` 以 ZIP 容器與 JSON 儲存，便於版本遷移與資產管理。

---

## 2. 技術棧

| 項目 | 版本／選擇 | 說明 |
| --- | --- | --- |
| 語言 | C# / .NET 10 | Nullable、file-scoped namespace、ImplicitUsings |
| UI | WinUI 3（Windows App SDK 2.6.5） | 原生 Fluent 控制項與 XAML 場景圖 |
| 目標框架 | `net10.0-windows10.0.19041.0` | 最低支援 Windows 10 1809（17763） |
| MVVM | CommunityToolkit.Mvvm 8.4.2 | `ObservableObject` 與手寫 `SetProperty` |
| DI | Microsoft.Extensions.DependencyInjection 10.0.14 | `ServiceCollection` 手動建置 |
| 日誌 | Microsoft.Extensions.Logging 10.0.14 | 自訂檔案 provider |
| 序列化 | System.Text.Json | CamelCase、列舉字串、多型 `type` |
| 容器 | System.IO.Compression | ZIP 儲存與載入 |
| 測試 | xUnit 2.9.3 | `MyDrawVector.Core.Tests` |
| 套件管理 | Central Package Management | `Directory.Packages.props` |

---

## 3. 專案結構與相依

```text
MyDrawVector.slnx
├── src/MyDrawVector.Core/          net10.0，無 UI 相依
│   ├── Models/                     文件、圖層、物件、列舉
│   ├── Geometry/                   變形、矩形、格線吸附
│   ├── History/                    復原歷史
│   └── Serialization/              JSON 與 ZIP 檔案格式
├── src/MyDrawVector/               net10.0-windows，WinUI 3
│   ├── App.xaml / App.xaml.cs      啟動、單一實例、DI、日誌
│   ├── MainWindow.xaml / .cs       主視窗、渲染、輸入、屬性面板
│   ├── CanvasHostControl.cs        工作區裁切與游標
│   ├── EditorTool.cs               工具列舉
│   ├── ViewModels/                 EditorViewModel
│   ├── Services/                   設定、檔案、自動儲存、資產、關聯、主題
│   ├── Controls/                   NumericField、ToolPathIcon
│   ├── Helpers/                    色彩、色盤、轉換器
│   ├── Strings/zh-Hant/            繁體中文字串資源
│   ├── Assets/                     圖示
│   └── app.manifest                DPI 與支援 OS 宣告
└── tests/MyDrawVector.Core.Tests/  xUnit，只測 Core
```

### 3.1 相依方向

```text
MyDrawVector (UI) ──► MyDrawVector.Core
MyDrawVector.Core.Tests ──► MyDrawVector.Core
```

Core 不參考 WinUI、Windows SDK 或 App 專案，確保可在一般 .NET 測試程序中執行。

### 3.2 平台設定

`src/MyDrawVector/MyDrawVector.csproj` 目前設定：

- `Platforms`：`x64;ARM64`
- `RuntimeIdentifiers`：`win-x64;win-arm64`
- `PlatformTarget`：依 `RuntimeIdentifier` 或 `Platform` 決定 `x64`／`arm64`
- `WindowsAppSDKSelfContained=false`
- `WindowsPackageType=None`
- `PublishSingleFile=false`
- `PublishTrimmed=false`

發行時由 RID 決定原生資產架構；ARM64 發行需使用 `-r win-arm64`。

---

## 4. 分層架構

| 層 | 專案 | 責任 |
| --- | --- | --- |
| Domain | Core | 文件模型、幾何、變形、歷史、序列化、ZIP 格式 |
| Application | App | ViewModel、選取、工具狀態、自動儲存、設定、檔案流程 |
| Infrastructure | App | 檔案存取、圖片解碼、剪貼簿、檔案關聯、日誌 |
| Presentation | App | XAML、渲染、輸入、對話框、屬性面板 |

> 現況中 `MainWindow.xaml.cs` 同時承擔 Presentation 與部分 Application／Infrastructure 協調工作，屬於大型 code-behind。Core 邊界則維持清楚。

---

## 5. 啟動與生命週期

### 5.1 進入點

WinUI XAML 編譯器會產生 `Program.Main`，流程為：

```text
Main → Application.Start → new App() → OnLaunched
```

`App` 建構子會註冊：

- `Application.UnhandledException`
- `AppDomain.CurrentDomain.UnhandledException`
- `TaskScheduler.UnobservedTaskException`

未處理例外會寫入日誌，避免無聲崩潰。

### 5.2 單一實例

`App.OnLaunched` 最先執行：

```text
AppInstance.FindOrRegisterForKey("MyDrawVector")
```

- 若目前不是主要實例：
  1. `RedirectActivationToAsync` 將啟用參數轉交給主要實例。
  2. 呼叫 `Exit()`，不建立視窗。
- 若為主要實例：
  1. 訂閱 `Activated` 事件。
  2. 初始化日誌與 DI。
  3. 處理命令列參數。
  4. 建立主視窗。

單一實例判定發生在日誌初始化之前，避免第二實例鎖住 `app.log`。

### 5.3 命令列參數

| 參數 | 行為 |
| --- | --- |
| `--register` | 建立／更新 `.mydraw` 關聯後直接結束 |
| `--unregister` | 移除 `.mydraw` 關聯後直接結束 |
| 其他非 `-` 開頭且存在的檔案路徑 | 啟動後開啟該檔案 |

環境變數 `MYDRAWVECTOR_DISABLE_ASSOC=1` 可停用啟動時自動建立關聯。

### 5.4 DI 與服務註冊

`App.xaml.cs` 以 `ServiceCollection` 註冊：

- `SettingsService`（Singleton）
- `FileAssociationService`（Singleton）
- `FileService`（Singleton）
- `ImageAssetStore`（Singleton）
- `AutosaveService`（Singleton）
- `EditorViewModel`（Singleton）
- `MainWindow`（Singleton）
- 日誌：`Microsoft.Extensions.Logging` + `FileLoggerProvider`

`App.Services` 為靜態 `IServiceProvider`，供啟動流程解析服務。

### 5.5 主視窗初始化

`MainWindow` 建構子依序：

1. 載入鉛筆樣式、顯示模式、主題顏色並套用。
2. `InitializeComponent()`。
3. 初始化工具按鈕、色盤、縮放滑桿。
4. 取得 `AppWindow`，建立自動儲存與刪除提示計時器。
5. 初始化視窗尺寸、位置、圖示與最小尺寸。
6. 註冊事件與系統色彩變更監聽。
7. 訂閱文件與選取變更事件。

`RootGrid_Loaded` 時：

1. 套用主題與 `RenderAll()`。
2. 延後執行 `FitToPage()`。
3. 啟動自動儲存計時器。
4. 嘗試恢復自動儲存；若無，再開啟啟動檔案。

### 5.6 關閉流程

```text
AppWindow.Closing
  ├─ 無未儲存變更 → 允許關閉
  └─ 有未儲存變更 → Cancel + PromptCloseAsync
                         ├─ 儲存 → SaveAsync
                         ├─ 不儲存 → 允許關閉（保留 autosave）
                         └─ 取消 → 留在視窗
```

`Closed` 時會停止計時器、取消事件訂閱、保存視窗位置與大小、保存鉛筆樣式，最後寫入設定。

---

## 6. 主要元件

### 6.1 Core

| 類別 | 檔案 | 責任 |
| --- | --- | --- |
| `Document` | `Core/Models/Document.cs` | 畫布、圖層、物件、資產；正規化與查詢 |
| `CanvasSettings` | `Core/Models/CanvasSettings.cs` | 畫布尺寸、背景、預設名稱 |
| `Layer` | `Core/Models/Layer.cs` | 圖層名稱、排序、顯示、鎖定 |
| `SceneObject` | `Core/Models/SceneObject.cs` | 物件抽象基底、命中測試、Clone |
| `ShapeObject` | `Core/Models/ShapeObject.cs` | 幾何圖形與樣式 |
| `TextObject` | `Core/Models/TextObject.cs` | 文字內容與字型樣式 |
| `PathObject` | `Core/Models/PathObject.cs` | 自由筆畫正規化點與線段命中 |
| `ImageObject` | `Core/Models/ImageObject.cs` | 圖片資產參照 |
| `ObjectTransform` | `Core/Geometry/ObjectTransform.cs` | 中心、尺寸、旋轉、翻轉、矩陣 |
| `GridSnap` | `Core/Geometry/GridSnap.cs` | 格線吸附計算 |
| `RectD` | `Core/Geometry/RectD.cs` | 矩形值型別 |
| `UndoManager` | `Core/History/UndoManager.cs` | 快照式復原與合併 |
| `DocumentSerializer` | `Core/Serialization/DocumentSerializer.cs` | JSON 序列化與反序列化 |
| `DocumentPackageService` | `Core/Serialization/DocumentPackageService.cs` | `.mydraw` ZIP 儲存與載入 |

### 6.2 App

| 類別 | 檔案 | 責任 |
| --- | --- | --- |
| `App` | `App.xaml.cs` | 啟動、單一實例、DI、日誌、例外、關聯 |
| `MainWindow` | `MainWindow.xaml.cs` | 主視窗、渲染、輸入、屬性面板、檔案流程 |
| `EditorViewModel` | `ViewModels/EditorViewModel.cs` | 文件狀態、選取、歷史、圖層與物件操作 |
| `SettingsService` | `Services/SettingsService.cs` | 設定載入／儲存、舊版遷移、最近檔案 |
| `AutosaveService` | `Services/AutosaveService.cs` | 自動儲存檔與中繼資料 |
| `FileService` | `Services/FileService.cs` | 文件存開封裝 |
| `FileAssociationService` | `Services/FileAssociationService.cs` | HKCU 檔案關聯 |
| `ImageAssetStore` | `Services/ImageAssetStore.cs` | 圖片資產去重、解碼與快取 |
| `FileLoggerProvider` | `Services/FileLoggerProvider.cs` | 檔案日誌與輪替 |
| `AppThemePalette` | `Services/AppThemePalette.cs` | 10 種主題色目錄 |
| `ShapeToolStyle` | `Services/ShapeToolStyle.cs` | 單一圖形工具的樣式記憶 |
| `TextToolStyle` | `Services/TextToolStyle.cs` | 文字工具樣式記憶 |
| `NumericField` | `Controls/NumericField.xaml.cs` | 自訂數值欄位 |
| `ToolPathIcon` | `Controls/ToolPathIcon.xaml.cs` | 繼承前景色的工具圖示 |
| `CanvasHostControl` | `CanvasHostControl.cs` | 工作區裁切與游標設定 |

---

## 7. 文件模型

### 7.1 物件繼承

```text
SceneObject (abstract)
├── ShapeObject      矩形、圓角矩形、橢圓、直線、箭頭、多邊形、星形、對話框
├── TextObject       文字、字型、字重、對齊、行距
├── ImageObject      資產 ID
└── PathObject       自由筆畫正規化點
```

`SceneObject` 共同欄位：

- `Id`：GUID `"N"` 格式
- `Name`：顯示名稱
- `LayerId`：所屬圖層
- `ZIndex`：圖層內順序
- `Transform`：`ObjectTransform`
- `Opacity`：0～1
- `IsVisible`、`IsLocked`
- `WorldBounds`：由 Transform 計算的 AABB（不序列化）

### 7.2 圖層與畫布

- `Document.Layers`：圖層集合，`Order` 決定先後。
- `Document.Objects`：所有物件，透過 `LayerId` 與 `ZIndex` 排序。
- `Document.Assets`：圖片資產中繼資料。
- `Document.EnsureLayers()`：
  - 補齊 null 集合。
  - 建立預設「圖層 1」。
  - 修正空白或無效的 `LayerId`。
  - 重排圖層 `Order` 與每層 `ZIndex`。
  - 支援舊版沒有圖層欄位的文件。

### 7.3 圖形種類

`ShapeKind`：

- `Rectangle`
- `RoundedRectangle`
- `Ellipse`
- `Line`
- `Arrow`
- `Triangle`
- `Diamond`
- `Pentagon`
- `Hexagon`
- `Star`
- `SpeechBubble`

---

## 8. 幾何與變形

### 8.1 物件矩陣

物件以中心點為原點，`ObjectTransform.ToMatrix()` 組合順序為：

```text
M = Translate(-Width/2, -Height/2)
  * Scale(FlipH ? -1 : 1, FlipV ? -1 : 1)
  * Rotate(Rotation)
  * Translate(CenterX, CenterY)
```

使用 `System.Numerics.Matrix3x2`。XAML 端以 `MatrixTransform` 套用。

### 8.2 座標轉換

- 世界 → 區域：物件矩陣反轉後轉換點。
- 區域 → 世界：物件矩陣正向轉換。
- 螢幕 → 世界：檢視矩陣反轉後轉換。
- 世界 → 螢幕：檢視矩陣正向轉換。

### 8.3 命中測試

`SceneObject.HitTest`：

1. 不可見或鎖定 → `false`。
2. 世界座標轉為物件區域座標。
3. 呼叫各子類的 `ContainsLocal`。

各子類：

- 矩形、多邊形、線、箭頭：區域矩形範圍。
- 橢圓：橢圓方程式。
- 自由筆畫：先檢查矩形範圍，再計算點到線段距離。

`MainWindow.HitTestObject` 由最高圖層往下、每層由最高 Z 序往下尋找第一個命中物件。

### 8.4 縮放與旋轉

- 縮放：將拖曳量投影到物件旋轉後的軸，依控制點調整寬高與中心。
- 鎖定比例：取最大縮放比，確保不小於 8 DIP。
- 旋轉：以物件中心計算角度差；`Shift` 吸附 15°。
- 翻轉：切換區域軸 scale 正負號，保留旗標。

### 8.5 格線吸附

- 格線間距 20 DIP，每 100 DIP 為主格線。
- 吸附容差 8 DIP。
- 套用於移動、縮放與建立圖形。
- 拖曳時按住 `Alt` 可暫時停用。

---

## 9. 渲染架構

### 9.1 場景層

`MainWindow.xaml` 的畫布區包含多層 Canvas：

| 層 | 用途 |
| --- | --- |
| `CanvasHost` | 輸入與工作區裁切 |
| `SceneRoot` | 套用檢視矩陣的文件座標根 |
| `PageShadow` | 頁面陰影 |
| `PageBackground` | 頁面背景 |
| `CheckerCanvas` | 透明背景棋盤格 |
| `GridCanvas` | 格線 |
| `ObjectCanvas` | 物件視覺元素 |
| `PreviewCanvas` | 建立圖形、框選、鉛筆預覽 |
| `OverlayCanvas` | 選取框、控制點、旋轉手柄 |
| `ActionCanvas` | 浮動選取工具列 |
| `CanvasHint` | 空文件提示 |

### 9.2 視覺元素快取

`MainWindow` 以物件 ID 快取視覺元素：

- `ShapeObject` → `Rectangle`、`Ellipse` 或 `Path`
- `TextObject` → `TextBlock`
- `ImageObject` → `Image`
- `PathObject` → `Path`

`RenderAll()` 會：

1. 更新頁面尺寸、背景、裁切與格線。
2. 依 `Document.OrderedObjects()` 更新或建立視覺元素。
3. 依圖層可見性設定 `Visibility`。
4. 依扁平順序設定 `Canvas.ZIndex`。
5. 移除已不存在物件的視覺元素。
6. 更新空文件提示、overlay、屬性面板與狀態列。

### 9.3 檢視矩陣

```text
ViewMatrix = Scale(zoom) * Translate(pan)
```

- `SceneRoot.RenderTransform` 每次重新指派新的 `MatrixTransform`。
- 這是刻意設計：WinUI 3 對就地修改 `MatrixTransform.Matrix` 不一定會即時重繪。
- `PreviewCanvas` 會依縮放後的頁面範圍裁切。
- 平移會限制在合理範圍，確保畫紙至少保留 40 DIP 可見。

### 9.4 Overlay

- 選取框與控制點繪製在螢幕座標 overlay，不隨縮放變粗。
- 單選：多邊形選取框、8 個控制點、旋轉連接線與手柄。
- 多選：虛線外框，不顯示個別控制點。
- 控制點半徑 9 DIP，旋轉手柄半徑 12 DIP。
- 浮動工具列依 `RasterizationScale` 與多選狀態快取尺寸。

---

## 10. 互動與工具狀態機

### 10.1 拖曳模式

```text
DragMode = None | Pan | Move | Resize | Rotate | Marquee | Shape | Pencil
```

Pointer 事件集中由 `CanvasHost` 處理，物件視覺元素不參與命中測試。

### 10.2 PointerPressed 流程

1. 將焦點交給 `FocusSink`。
2. 中鍵／右鍵／平移工具／空白鍵 → 開始平移。
3. 單選時先檢查控制點與旋轉手柄。
4. 命中物件 → 切換目前圖層、處理多選、開始移動。
5. 未命中 → 依工具執行框選、文字、鉛筆或建立圖形。

### 10.3 手勢

- 移動、縮放、旋轉開始時會序列化目前文件為 `_gestureBeforeJson`。
- 放開時若有變更，推入一筆復原紀錄。
- `Esc` 或捕捉遺失時取消互動，必要時還原手勢前快照。

### 10.4 文字與圖片

- 文字使用 `ContentDialog` 編輯，取消時不建立物件。
- 圖片使用 `Microsoft.Windows.Storage.Pickers` 選擇檔案，並以旗標防止重複開啟。
- 圖片與 `.mydraw` 支援拖放。

---

## 11. 復原與重做

### 11.1 快照式歷史

`EditorViewModel.History` 是 `UndoManager`：

- 以精簡 JSON 字串保存文件快照。
- 上限 100 步。
- `Push` 會清空 redo 堆疊。
- `PushCoalesced` 會依 coalesce key 與時間窗複合連續操作。

### 11.2 合併策略

- 合併時間窗：700 ms。
- 常見 coalesce key：`opacity`、`fill`、`stroke`、`strokeWidth`、`cornerRadius`、`fontSize`、`textColor`、`fontWeight`、`lineSpacing`、`canvas-background`、`pencilStroke`、`pencilWidth`。
- 拖曳手勢在放開時只建立一筆紀錄。

### 11.3 選取還原

復原／重做前會記住目前選取的物件 ID 與圖層 ID，套用快照後再依 ID 還原選取。

### 11.4 ChangeVersion

`EditorViewModel.ChangeVersion` 是單調遞增計數：

- `MarkDirty()`、`Undo()`、`Redo()` 時遞增。
- 自動儲存會比對 `_lastAutosavedVersion`，內容未變就不重寫。

---

## 12. 序列化與檔案格式

### 12.1 ZIP 容器

`.mydraw`：

```text
manifest.json
document.json
assets/
  img_<hash>.png
```

`manifest.json`：

```json
{
  "schema": "mydrawvector",
  "version": 2,
  "appVersion": "1.6.12",
  "created": "2026-11-11T12:00:00+08:00",
  "modified": "2026-11-11T12:00:00+08:00"
}
```

### 12.2 document.json

`DocumentSerializer` 選項：

- `PropertyNamingPolicy = CamelCase`
- `JsonStringEnumConverter`
- 允許註解與尾逗號
- 一般輸出縮排；精簡輸出用於歷史快照

多型以 `"type"` 判別：

- `"shape"`
- `"text"`
- `"image"`
- `"path"`

### 12.3 原子寫入

`DocumentPackageService.SaveAsync`：

1. 寫入 `path + ".tmp"`。
2. 完成後 `File.Move(tempPath, path, overwrite: true)`。
3. 失敗時嘗試刪除暫存檔。

### 12.4 安全檢查

- 資產檔名不可空白、不可包含路徑分隔或 `..`。
- `Path.GetFileName(fileName)` 必須等於原字串。
- 缺少 `document.json` 會視為損壞檔案。
- 缺少資產時跳過，不讓整份文件開啟失敗。

### 12.5 版本遷移

- 容器版本記錄在 `manifest.json`。
- 文件層沒有獨立版本欄位。
- `Document.EnsureLayers()` 是主要遷移點，處理舊版沒有圖層的文件。
- 目前未使用 `JsonExtensionData` 保留未知欄位。

---

## 13. 圖片資產

`ImageAssetStore`：

- 以 SHA-256 計算內容雜湊，取前 16 位十六進位作為 `img_<hash>`。
- 同一內容只保存一份。
- 以 `BitmapDecoder` 取得像素尺寸。
- 以 `BitmapImage` 建立顯示用點陣圖。
- 支援 `.png`、`.jpg`、`.jpeg`、`.bmp`、`.gif`；其他格式以 `.png` 儲存。
- `StoredImage` 同時保留原始位元組與 `BitmapImage`。

---

## 14. 設定、主題與日誌

### 14.1 設定

- 路徑：`%AppData%\MyDrawVector\settings.json`。
- 內容：視窗位置與大小、格線、吸附、自動選取工具、顯示模式、主題顏色、各工具樣式、最近檔案。
- 儲存採暫存檔 + 取代。
- 舊版 `IsDarkMode` 會在載入時轉為 `ThemeMode = "Dark"`。
- 每個 `ShapeKind` 都有獨立的 `ShapeToolStyle`；缺少時由舊共用欄位建立。

### 14.2 顯示模式與主題顏色

- 顯示模式：`System`、`Light`、`Dark`。
- 跟隨系統時監聽 `UISettings.ColorValuesChanged`。
- 主題顏色共 10 種，直接更新 WinUI `XamlControlsResources` 的 accent 筆刷。
- 高對比模式不覆寫系統強調色。

### 14.3 日誌

- 路徑：`%LocalAppData%\MyDrawVector\Logs\app.log`。
- 使用 `FileLoggerProvider`，自動 flush。
- 超過 1 MB 輪替為 `app.log.1`。
- 未處理例外會寫入完整堆疊。

---

## 15. 無障礙與國際化

- 預設語言 `zh-Hant`。
- 使用者可見字串集中於 `Strings/zh-Hant/Resources.resw`，以 `x:Uid` 或 `ResourceLoader` 取得。
- 工具按鈕、屬性面板按鈕與數值欄位提供 `AutomationProperties.Name` 與工具提示。
- 支援鍵盤快捷鍵、Tab 循環選取、方向鍵微調與 Esc 取消。
- 高對比模式使用系統資源。
- 尚未以 Narrator 完成完整流程驗證。

---

## 16. 效能考量

- 視覺元素依物件 ID 快取，避免整頁重建。
- 筆刷以正規化 hex 快取 `SolidColorBrush`。
- 格線依尺寸與顯示狀態快取。
- 自動儲存以 `ChangeVersion` 去重。
- 圖片顯示使用 `BitmapImage`，資產以內容雜湊去重。
- 匯出 PNG 使用記憶體編碼後寫檔。
- 目前屬性面板調整仍可能整份更新；大型文件可再優化為只更新選取物件。

---

## 17. 安全性

- 完全離線，不需要網路權限。
- 檔案關聯只寫入 `HKCU\Software\Classes`。
- 開啟 ZIP 時檢查資產路徑，降低 zip-slip 風險。
- 不執行文件中的程式碼或外部指令。
- 日誌不記錄作品內容或圖片資料。
- 原始碼與版本控制不含密鑰；機密應使用環境變數或 Windows 認證管理員。

---

## 18. 測試策略

`tests/MyDrawVector.Core.Tests` 目前涵蓋：

- `GeometryTests`：變形矩陣、命中測試、格線吸附。
- `LayerTests`：圖層正規化、舊版 JSON 遷移。
- `SerializationTests`：物件往返、列舉、未知欄位行為。
- `DocumentPackageTests`：ZIP 儲存／載入、資產、損壞檔案。
- `UndoManagerTests`：復原／重做、上限、合併。

測試只針對 Core，UI 流程仍需手動驗證。建議手動測試清單：

- x64 與 ARM64 實機啟動。
- 125%／150%／200% DPI。
- 淺色／深色／高對比。
- 中文 IME。
- 500 物件縮放平移。
- 大型圖片插入與縮放。
- 存檔／開啟／復原／重做。
- 當機恢復。
- 檔案關聯與單一實例。
- PNG 透明與 2× 匯出。

---

## 19. 技術債與已知限制

- `MainWindow.xaml.cs` 約 5,000 行，承擔過多責任，後續可拆分為 Renderer、InputController、InspectorController 等元件。
- 未使用 `Microsoft.Extensions.Hosting`、`IOptions` 與 `appsettings.json`。
- 未使用 `RelayCommand`；多數事件仍以 code-behind 處理。
- 樣式與資源直接內嵌在 `MainWindow.xaml`，未拆分到 `Styles/`。
- 文件 JSON 未保留未知欄位，跨版本相容性有限。
- 屬性面板更新仍可能造成不必要的重繪。
- 高 DPI、Windows 10 1809、高對比與 ARM64 實機測試仍不完整。
- 尚未實作群組、智慧參考線、範本、均分、跨圖層搬移、SVG／JPG／PDF 匯出與列印。

---

## 20. 相關文件

- [使用指南](USER_GUIDE.md)
- [系統設計文件](DESIGN.md)
- [功能規格](FEATURE_SPEC.md)
- [開發與發行指南](DEVELOPMENT.md)
- [開發紀錄](DEV_LOG.md)
- [版本異動](../CHANGELOG.md)
- [授權](../LICENSE)
