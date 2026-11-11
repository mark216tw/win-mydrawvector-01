# MyDrawVector（我的向量畫布）

給國小、國中、高中生使用的 Windows 向量繪圖軟體。以 WinUI 3 與 .NET 10 開發，採免安裝可攜式資料夾發行，不需要 MSIX 或系統管理員權限。

[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-0078D4)](https://github.com/mark216tw/win-mydrawvector-01)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## 專案說明

MyDrawVector 是一套以文件為中心、完全離線的向量繪圖工具。使用者可以在 A4、A3、Letter 或 16:9 畫布上建立幾何圖形、文字、圖片與自由筆畫，並透過圖層、對齊、變形與復原功能完成學習單、海報、卡片與課堂作品。

- 文件格式 `.mydraw` 為 ZIP 容器，內含 `manifest.json`、`document.json` 與圖片資產。
- 所有編輯都在本機完成，不上傳作品、圖片或使用紀錄。
- 發行版本提供 **x64** 與 **ARM64** 兩種非 self-contained 可攜式 zip，不安裝系統服務、不提供安裝程式。

## 功能特色

### 畫布與檢視

- A4 橫式預設，可切換 A4 橫／直、A3、Letter 與 16:9。
- 滾輪以游標為中心縮放（10%～800%）、適合視窗、100% 重設。
- 空白鍵拖曳、中鍵拖曳、右鍵拖曳與平移工具。
- 頁面陰影、工作區裁切、畫紙外內容不顯示也不匯出。
- 透明畫布以棋盤格顯示，匯出 PNG 時可保留透明度。

### 圖形與樣式

- 矩形、圓角矩形、橢圓、直線、箭頭、三角形、菱形、五邊形、六邊形、星形、對話框。
- 20 色快速填色與邊框色盤、自訂顏色、透明度、粗細、虛線／點線、圓角。
- 「啟用填色」與「啟用邊框」開關。
- 每個圖形工具分別記住上次使用的樣式。

### 文字、圖片與自由筆畫

- 文字：插入、雙擊編輯、字型、字級、字重、粗體、斜體、底線、顏色、對齊、行距、換行。
- 圖片：剪貼簿貼上、插入檔案、拖放、替換圖片、等比例縮放。
- 自由筆畫：鉛筆工具建立可移動、縮放、旋轉與翻轉的向量路徑，並可調整線條顏色、粗細與樣式。

### 選取、變形與物件管理

- 單選、多選、框選、拖曳移動、8 個縮放控制點、旋轉手柄。
- Shift 鎖定比例、Shift 旋轉每 15° 吸附、方向鍵微調 1／10 DIP。
- 水平／垂直翻轉、0／90／180／270 快速角度。
- 物件順序、對齊（左中右、上中下）、圖層清單與目前圖層物件清單。
- 圖層新增、改名、刪除、上下移動、顯示／隱藏、鎖定。

### 檔案、復原與介面

- `.mydraw` 儲存／開啟、最近檔案、自動儲存與當機恢復、單一實例。
- 第一次啟動自動建立 `.mydraw` 檔案關聯（寫入 `HKCU\Software\Classes`，不需要系統管理員權限）。
- 快照式復原／重做至少 100 步；連續拖曳與數值連按會合併為一筆。
- 內部物件剪貼簿、外部圖片與純文字貼上。
- 兩層圖示工具列、可收合屬性面板（物件／圖層／畫布三個頁籤）、浮動選取工具列、狀態列。
- 跟隨系統／淺色／深色模式，以及 10 種可即時套用的主題顏色。
- 視窗位置與大小記憶、最小視窗尺寸、鍵盤操作與螢幕閱讀器名稱。

## 系統需求

| 項目 | 需求 |
| --- | --- |
| 作業系統 | Windows 10 1809（build 17763）以上 |
| 架構 | x64 或 ARM64（請下載對應版本） |
| 執行環境 | .NET 10 Desktop Runtime（x64 或 ARM64） |
| 架構相依 | Windows App SDK 2.6.5 Runtime（x64 或 ARM64） |
| 權限 | 一般使用者權限即可；檔案關聯只寫入目前使用者登錄區 |

本專案發行的是 **非 self-contained** 版本，執行前必須安裝上述兩個執行環境。若需要免安裝執行環境，可自行以 self-contained 模式發行。

## 下載與執行

1. 前往 [Releases](https://github.com/mark216tw/win-mydrawvector-01/releases/latest) 下載：
   - `MyDrawVector-1.6.12-portable-win-x64.zip`
   - `MyDrawVector-1.6.12-portable-win-arm64.zip`
2. 解壓縮到任意資料夾。
3. 安裝對應架構的 .NET 10 Desktop Runtime 與 Windows App SDK 2.6.5 Runtime。
4. 執行資料夾內的 `MyDrawVector.exe`。

> 可攜式版本是整個資料夾，請連同 `Assets`、DLL 與 `MyDrawVector.pri` 一起保留，不要只複製 exe。

第一次啟動會自動建立 `.mydraw` 檔案關聯；之後可直接雙擊 `.mydraw` 檔案開啟。若搬移資料夾後關聯失效，重新執行一次 exe，或由「設定 → 偏好設定 → 建立／更新檔案關聯」修正。

## 快速開始

1. 由左側工具列選擇圖形工具，在畫布上拖曳建立圖形；按住 Shift 可畫正方形或正圓。
2. 使用右側「物件」頁籤調整填色、邊框、粗細、透明度、圓角與旋轉。
3. 使用「圖層」頁籤管理圖層與物件順序；使用「畫布」頁籤切換尺寸、背景、格線與吸附。
4. 按 `Ctrl+S` 儲存為 `.mydraw`；按 `Ctrl+O` 開啟既有文件。
5. 由「檔案 → 匯出 PNG」輸出圖片，可選擇 1×／2×／3× 與是否保留透明背景。

完整操作說明請參閱 [使用指南](docs/USER_GUIDE.md)。

## 操作介面

- **選單列**：檔案、編輯、設定、說明。
- **左側工具列**：復原／重做、選取、圖形、文字、插入圖片、鉛筆、平移與更多圖形。
- **畫布區**：頁面、格線、透明棋盤格、物件、預覽層、選取控制點與浮動工具列。
- **右側屬性面板**：
  - 物件：樣式、透明度、旋轉與翻轉、文字或圖片屬性。
  - 圖層：圖層清單、目前圖層物件、顯示與鎖定。
  - 畫布：格線、吸附、自動選取工具、畫布尺寸與背景。
- **狀態列**：畫布尺寸、選取摘要、格線／吸附狀態、縮放比例與自動儲存時間。

## 快捷鍵

| 快捷鍵 | 功能 |
| --- | --- |
| `Ctrl+Z` / `Ctrl+Y` / `Ctrl+Shift+Z` | 復原／重做／重做 |
| `Ctrl+C` / `Ctrl+V` / `Ctrl+X` | 複製／貼上／剪下 |
| `Ctrl+D` | 再製 |
| `Ctrl+A` | 全選 |
| `Tab` / `Shift+Tab` | 切換選取物件 |
| `Ctrl+S` / `Ctrl+Shift+S` | 儲存／另存新檔 |
| `Ctrl+O` / `Ctrl+N` | 開啟／新增 |
| `V` / `R` / `E` / `L` / `T` / `I` / `P` / `H` | 選取／矩形／橢圓／直線／文字／插入圖片／鉛筆／平移 |
| `Ctrl+0` / `Ctrl+1` | 縮放 100%／適合視窗 |
| `Ctrl++` / `Ctrl+-` | 放大／縮小 |
| `Delete` | 刪除選取物件 |
| 方向鍵 / `Shift`+方向鍵 | 微調 1 / 10 DIP |
| 滾輪 | 以游標為中心縮放 |
| `空白鍵`+拖曳、中鍵拖曳、右鍵拖曳 | 平移畫布 |
| `Esc` | 取消目前操作、清除選取 |
| `Shift`+旋轉 | 每 15° 吸附 |
| `Shift`+拖曳圖形 | 正方形／正圓 |
| 拖曳時按住 `Alt` | 暫時關閉格線吸附 |

## 檔案格式

`.mydraw` 是 ZIP 容器：

```text
manifest.json      schema、版本與建立／修改時間
document.json      畫布、圖層、物件、樣式與資產參照
assets/            圖片資產（原始位元組）
```

- 文件以 `System.Text.Json` 序列化，列舉使用字串。
- 儲存採先寫暫存檔再取代的原子寫入方式。
- 開啟時會檢查資產檔名，避免 ZIP 路徑穿越。
- 舊版沒有圖層欄位的文件會在載入時自動建立預設圖層並整理 Z 序。

詳細格式請參閱 [系統設計文件](docs/DESIGN.md#檔案格式設計)。

## 專案結構

```text
src/MyDrawVector.Core/          文件模型、幾何、序列化、ZIP 檔案格式、復原歷史
src/MyDrawVector/               WinUI 3 應用程式、渲染、工具、設定、檔案關聯
tests/MyDrawVector.Core.Tests/  Core 單元測試
docs/                           使用、架構、設計、開發與功能規格文件
tools/make-icon.ps1             產生多尺寸 app.ico
```

更完整的說明請參閱 [系統架構與技術文件](docs/ARCHITECTURE.md)。

## 建置與測試

需求：.NET 10 SDK、Windows 10 1809 以上。

```powershell
dotnet build MyDrawVector.slnx -c Debug
dotnet test MyDrawVector.slnx -c Debug
dotnet format MyDrawVector.slnx --verify-no-changes
```

## 發行可攜式版本

### x64

```powershell
dotnet publish src\MyDrawVector\MyDrawVector.csproj -c Release -r win-x64 --self-contained false `
  -p:WindowsAppSDKSelfContained=false -p:WindowsPackageType=None `
  -p:PublishSingleFile=false -p:PublishTrimmed=false `
  -o publish\MyDrawVector-portable-win-x64
```

### ARM64

```powershell
dotnet publish src\MyDrawVector\MyDrawVector.csproj -c Release -r win-arm64 --self-contained false `
  -p:WindowsAppSDKSelfContained=false -p:WindowsPackageType=None `
  -p:PublishSingleFile=false -p:PublishTrimmed=false `
  -o publish\MyDrawVector-portable-win-arm64
```

`MyDrawVector.csproj` 內含 `CopyAppPriToPublish` target，會把 `MyDrawVector.pri` 複製到發行資料夾；缺少這個檔案時 Release 版會在啟動時發生 XAML 解析錯誤。

## 文件

- [使用指南](docs/USER_GUIDE.md)
- [系統架構與技術文件](docs/ARCHITECTURE.md)
- [系統設計文件](docs/DESIGN.md)
- [功能規格](docs/FEATURE_SPEC.md)
- [開發與發行指南](docs/DEVELOPMENT.md)
- [開發紀錄](docs/DEV_LOG.md)
- [版本異動](CHANGELOG.md)
- [版本說明](RELEASE_NOTES.md)
- [貢獻指南](CONTRIBUTING.md)

## 隱私

MyDrawVector 完全離線運作，不會上傳作品、圖片、檔案路徑或使用紀錄。設定與日誌只寫入目前使用者的本機資料夾：

- 設定：`%AppData%\MyDrawVector\settings.json`
- 日誌：`%LocalAppData%\MyDrawVector\Logs\app.log`
- 自動儲存：`%LocalAppData%\MyDrawVector\Autosave`

## 已知限制

- 尚未實作群組／解群組、智慧參考線、範本庫、均分與物件跨圖層搬移。
- 尚未實作 SVG／JPG／PDF 匯出與列印。
- 文字編輯使用對話框；旋轉文字編輯時不會即時以旋轉角度顯示。
- 字型清單以常用字型為主；文件使用其他字型時會加入清單並以系統字型呈現。
- 尚未支援雙指縮放、觸控筆壓感、圖片裁切。
- 自動儲存每 60 秒寫入一次；若在第一次自動儲存前當機，可能無法恢復最新變更。
- ARM64 版本已完成建置與封裝，但尚未在實體 ARM64 裝置上完成完整手動測試。

## 授權

本專案採用 [MIT License](LICENSE)。

Copyright (c) 2026 mark216tw
