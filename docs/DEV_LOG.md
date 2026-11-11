# 開發紀錄

最新紀錄放最上面。每次開發工作都要更新，不要事後補寫。

---

## 2026-11-11 - v1.6.12

### 目標
補齊繁體中文專案文件與 MIT License，新增 ARM64 非 self-contained 發行，並建立 GitHub 公開儲存庫與 Release。

### 變更
- `README.md`：改寫為完整專案說明，包含功能、系統需求、下載、操作介面、快捷鍵、檔案格式、建置、發行與文件索引。
- `docs/USER_GUIDE.md`：新增使用指南，涵蓋安裝、介面、檔案、畫布、圖形、文字、圖片、鉛筆、選取變形、圖層、剪貼簿、復原、自動儲存、設定、檔案關聯與疑難排解。
- `docs/ARCHITECTURE.md`：新增系統架構與技術文件，說明分層、啟動流程、主要元件、文件模型、幾何、渲染、互動、復原、序列化、資產、設定、日誌、平台與測試。
- `docs/DESIGN.md`：新增系統設計文件，說明設計原則、使用案例、模組設計、資料設計、關鍵流程、演算法、狀態機、檔案格式、錯誤處理與設計決策。
- `docs/FEATURE_SPEC.md`：改寫為 v1.6.12 現況功能規格，整理已實作、未實作、介面、互動、物件模型、發行與驗收清單。
- `docs/DEVELOPMENT.md`：新增開發與發行指南，包含環境、建置、測試、格式、執行、除錯、x64／ARM64 發行、zip 打包、版本規則與貢獻流程。
- `docs/PORTABLE_README.txt`：新增可攜式版使用說明，會放入 release zip。
- `CONTRIBUTING.md`、`LICENSE`：新增貢獻指南與 MIT License。
- `src/MyDrawVector/MyDrawVector.csproj`：`Platforms` 改為 `x64;ARM64`，`RuntimeIdentifiers` 改為 `win-x64;win-arm64`，`PlatformTarget` 依 RID／Platform 決定。
- `Directory.Build.props`、`app.manifest`：版本更新為 1.6.12。
- `CHANGELOG.md`、`RELEASE_NOTES.md`：同步版本與說明。

### 問題與解法
- ARM64 發行需要同時調整 `Platforms`、`RuntimeIdentifiers` 與 `PlatformTarget`；若只改 `-r win-arm64`，仍可能因固定的 `PlatformTarget` 產出錯誤架構。改以條件式 `PlatformTarget` 依 `RuntimeIdentifier` 與 `Platform` 選擇。
- 可攜式版必須包含 `MyDrawVector.pri`、`Assets` 與所有 DLL；建立 zip 前逐項確認，避免只複製 exe。
- 舊 `publish` 資料夾內的說明檔版本老舊且檔名編碼異常；改以 `docs/PORTABLE_README.txt` 作為來源，重新產生 `使用說明.txt`。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug`：0 警告、0 錯誤。
- `dotnet test MyDrawVector.slnx -c Debug`：36 個測試通過。
- `dotnet format MyDrawVector.slnx --verify-no-changes`：通過。
- `dotnet publish` x64 與 arm64 非 self-contained 均成功，輸出含 `MyDrawVector.pri` 與 `Assets`。
- 以 PE header 檢查 `MyDrawVector.exe`：x64 輸出為 x64、arm64 輸出為 ARM64。
- 建立 x64 與 arm64 zip，內含 `使用說明.txt`。
- 待實機確認 ARM64 版本啟動與主要操作。

### 待辦
- 尚未在實體 ARM64 裝置完成完整手動測試。
- 尚未實測 Windows 10 1809、高 DPI 與高對比模式。

---

## 2026-11-11 - v1.6.11

### 目標
將「點擊物件自動選擇選取工具」從偏好設定移到屬性面板的「畫布」頁籤，讓畫布相關設定集中管理。

### 變更
- `MainWindow.xaml`：屬性頁「畫布」區塊新增 `AutoSelectToolToggle`。
- `MainWindow.xaml.cs`：`UpdateInspector` 同步開關狀態；新增 `AutoSelectToolToggle_Toggled` 立即儲存設定；偏好設定移除該開關。
- `Strings/zh-Hant/Resources.resw`：新增 `AutoSelectToolToggle.Header/OnContent/OffContent`，移除未使用的舊字串。
- `Directory.Build.props`、`app.manifest`：版本更新為 1.6.11。
- `CHANGELOG.md`、`RELEASE_NOTES.md`：同步版本與說明。

### 問題與解法
- 偏好設定內的開關原本只在按「套用」後才儲存；移到屬性頁後改為與顯示格線、吸附至格線一致，切換即生效並儲存。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug`：0 警告、0 錯誤。
- `dotnet test MyDrawVector.slnx -c Debug`：36 個測試通過。
- `dotnet format MyDrawVector.slnx --verify-no-changes`：通過。
- 待實機確認屬性頁切換後點擊物件會自動切回選取工具，且偏好設定不再顯示該選項。

### 待辦
- 尚未實測 Windows 10 1809 與高 DPI 縮放。

---

## 2026-11-11 - v1.6.10

### 目標
讓每個圖形工具分別記住自己的樣式，並讓文字工具記住文字樣式，避免不同工具互相覆蓋設定。

### 變更
- `Services/ShapeToolStyle.cs`：新增圖形工具樣式模型，包含填色、邊框、粗細、線條樣式、圓角與開關。
- `Services/TextToolStyle.cs`：新增文字工具樣式模型，包含字型、大小、粗細、斜體、底線、顏色、對齊與行距。
- `Services/AppSettings.cs`：新增 `ShapeToolStyles` 與 `TextStyle`，保留舊有圖形欄位作為遷移來源。
- `Services/SettingsService.cs`：載入設定時將舊共用圖形樣式補到每個 `ShapeKind`，並確保文字樣式存在。
- `MainWindow.xaml.cs`：圖形建立與屬性調整改用每個 `ShapeKind` 的樣式；文字建立、貼上與屬性調整改用文字工具樣式。
- `README.md`：更新各工具分開記憶樣式的說明。
- `Directory.Build.props`、`app.manifest`：版本更新為 1.6.10。
- `CHANGELOG.md`、`RELEASE_NOTES.md`：同步版本與說明。

### 問題與解法
- 原本所有圖形共用同一組 `_newShape*` 欄位，調整任一圖形都會影響其他工具；改以 `ShapeKind` 為 key 儲存樣式，並在建立圖形時讀取對應樣式。
- 文字新物件原本使用模型預設值，沒有記憶使用者調整；新增 `TextStyle` 並在文字屬性變更時同步寫回。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug`：0 警告、0 錯誤。
- `dotnet test MyDrawVector.slnx -c Debug`：36 個測試通過。
- `dotnet format MyDrawVector.slnx --verify-no-changes`：通過。
- 待實機確認各圖形工具樣式互不影響、文字新物件沿用樣式與舊設定遷移。

### 待辦
- 尚未實測 Windows 10 1809 與高 DPI 縮放。

---

## 2026-11-11 - v1.6.9

### 目標
調整文字顏色按鈕位置，讓它緊鄰字型粗細選單，並將透明畫布的棋盤格顏色調淡。

### 變更
- `MainWindow.xaml`：文字顏色按鈕與字型粗細下拉改為左對齊橫向排列，移除會將按鈕推到最右側的 `*` 欄位。
- `Helpers/CanvasBackgroundPalette.cs`：新增共用的棋盤格淺色與深色常數。
- `MainWindow.xaml.cs`：畫布棋盤格與透明背景色塊預覽改用共用常數，深色格由 `#E2E5E9` 調淡為 `#F2F3F5`，淺色格改為白色。
- `Directory.Build.props`、`app.manifest`：版本更新為 1.6.9。
- `CHANGELOG.md`、`RELEASE_NOTES.md`：同步版本與說明。

### 問題與解法
- 文字顏色按鈕原本位於 `Grid` 的 `Auto` 欄位，前方 `*` 欄位會把按鈕推到最右側；改用 `StackPanel` 左對齊排列，讓按鈕緊接在下拉選單旁。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug`：0 警告、0 錯誤。
- `dotnet test MyDrawVector.slnx -c Debug`：36 個測試通過。
- `dotnet format MyDrawVector.slnx --verify-no-changes`：通過。
- 待實機確認文字顏色按鈕位置與透明棋盤格深淺。

### 待辦
- 尚未實測 Windows 10 1809 與高 DPI 縮放。

---

## 2026-11-11 - v1.6.8

### 目標
擴充直線與箭頭的選取範圍為矩形，修正翻轉後旋轉手柄位置，讓字型大小加減鈕可長按連發，並簡化文字屬性版面。

### 變更
- `Models/ShapeObject.cs`：直線與箭頭的 `ContainsLocal` 改為矩形範圍判斷，移除線段距離計算。
- `MainWindow.xaml.cs`：旋轉手柄改為依 `FlipV` 選擇視覺上方邊緣，並同時回傳連接線起點；`RenderOverlay` 與 `HitTestHandle` 改用新的手柄座標。
- `MainWindow.xaml.cs`：`UpdateInspector` 不再先將所有屬性區塊設為 `Collapsed` 再顯示，改為一次依選取型別設定可見性，避免中斷 `RepeatButton` 的按住狀態。
- `MainWindow.xaml`：移除文字粗體按鈕；文字對齊下拉移除標題並移到斜體／底線右側；文字顏色按鈕與字型粗細改為同一列。
- `GeometryTests.cs`：更新直線選取測試為矩形範圍，新增箭頭案例與框內空白處可選取的驗證。
- `Directory.Build.props`、`app.manifest`：版本更新為 1.6.8。
- `CHANGELOG.md`、`RELEASE_NOTES.md`：同步版本與說明。

### 問題與解法
- 由下往上繪製的直線與箭頭會設定 `FlipV`，原本以物件區域座標 `-Y` 計算旋轉手柄，翻轉後落到視覺下方；改以翻轉狀態選擇上緣並將手柄往外偏移。
- 屬性面板每次更新都會先收合再顯示區塊，按住數值加減鈕時可能因可見性變更中斷連發；改為只在型別不符時收合。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug`：0 警告、0 錯誤。
- `dotnet test MyDrawVector.slnx -c Debug`：36 個測試通過。
- `dotnet format MyDrawVector.slnx --verify-no-changes`：通過。
- 待實機確認線條框內選取、翻轉後手柄位置、字型大小長按與文字屬性版面。

### 待辦
- 尚未實測 Windows 10 1809 與高 DPI 縮放。

---

## 2026-11-11 - v1.6.7

### 目標
將屬性面板分為物件、圖層、畫布三個頁籤；把工具列鉛筆按鈕移到矩形上方；補上鉛筆顏色與粗細設定、文字字型粗細設定，並讓文字選色視窗與填色、邊框一致。

### 變更
- `MainWindow.xaml`：屬性面板改用 `TabView`，分為物件、圖層、畫布三個頁籤；物件頁籤保留 `InspectorScroll` 名稱以沿用捲動位置保存。
- `MainWindow.xaml`：工具列鉛筆按鈕移到矩形按鈕上方。
- `MainWindow.xaml`：新增筆畫區塊，包含線條顏色、線條粗細、線條樣式；文字區塊新增字型粗細下拉；文字顏色 Flyout 改為預設色盤、關閉按鈕與同寬 `ColorPicker`。
- `MainWindow.xaml.cs`：新增鉛筆樣式欄位與事件處理，套用到繪製預覽與新筆畫；文字字重渲染與粗體按鈕雙向同步；在畫布選取物件時自動切到物件頁籤，從圖層清單點選則不切換。
- `Models/TextObject.cs`：新增 `FontWeight`，`0` 代表沿用舊文件的 `Bold` 設定。
- `Services/AppSettings.cs`：新增鉛筆線條顏色、粗細與樣式設定。
- `Strings/zh-Hant/Resources.resw`：新增頁籤、筆畫、字型粗細與文字顏色字串。
- `Directory.Build.props`、`app.manifest`：版本更新為 1.6.7。

### 問題與解法
- 屬性面板原本是單一捲動清單，改為頁籤後每個頁籤各自捲動；物件頁籤保留 `InspectorScroll` 名稱，避免修改既有捲動位置保存邏輯。
- 舊文件沒有 `FontWeight` 欄位，以 `0` 作為未設定值，渲染時退回 `Bold`，避免既有粗體文字失效。
- 鉛筆單點（未拖曳）仍建立小型橢圓物件，但改用目前鉛筆顏色與粗細。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug`：0 警告、0 錯誤。
- `dotnet test MyDrawVector.slnx -c Debug`：35 個測試通過。
- `dotnet format MyDrawVector.slnx --verify-no-changes`：通過。
- 待實機確認三頁籤切換、鉛筆預覽與選色 Flyout 在窄面板的呈現。

### 待辦
- 尚未實測 Windows 10 1809 與高 DPI 縮放。
- 鉛筆與文字預設樣式仍需先選取物件後才能調整。

---

## 2026-11-11 - v1.6.6

### 目標
調整填色與邊框選色 Flyout：縮小版面、提供關閉按鈕、預設色選取後不自動關閉，並隱藏底部輸入欄位。

### 變更
- `MainWindow.xaml`：填色與邊框 Flyout 標題列加入右上角 X 關閉按鈕，縮小色塊與間距，ColorPicker 固定寬度並隱藏預覽條。
- `MainWindow.xaml`：ColorPicker 關閉透明度與 HEX 文字輸入，並隱藏 More 按鈕；保留透明度滑桿。
- `MainWindow.xaml.cs`：預設色點選後不再呼叫 `Hide()`；新增填色／邊框 Flyout 關閉事件。
- `MainWindow.xaml.cs`：預設色塊按鈕由 38×38 縮為 32×32，內部色塊由 24×24 縮為 20×20。
- `Strings/zh-Hant/Resources.resw`：新增關閉按鈕的 ToolTip 與自動化名稱。
- `Directory.Build.props`、`app.manifest`：版本 1.6.6。
- `CHANGELOG.md`、`RELEASE_NOTES.md`：同步版本與說明。

### 問題與解法
- 選色視窗缺乏明確關閉方式 → 在標題列加入 X 圖示按鈕，維持緊湊版面。
- 點預設色會立即關閉 Flyout，無法連續比較顏色 → 移除自動 `Hide()`，改由關閉按鈕或點擊外部關閉。
- 選色器底部文字欄位佔空間 → 關閉透明度與 HEX 輸入，只保留色譜、色相／明度與透明度滑桿。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug`：0 警告、0 錯誤。
- `dotnet test MyDrawVector.slnx -c Debug`：Core 單元測試全數通過。
- `dotnet format MyDrawVector.slnx --verify-no-changes`：通過。
- 待實機確認：連續點選預設色、X 關閉、縮小後 Flyout 不超出畫面。

### 待辦
- 尚未實測 Windows 10 1809 與高 DPI 縮放。

---

## 2026-11-11 - v1.6.5

### 目標
將畫布設定集中到屬性頁，畫布背景改為常用固定色盤（含透明），並讓主題顏色即時套用、修正偏好設定捲軸重疊。

### 變更
- `Helpers/CanvasBackgroundPalette.cs`：新增畫布背景固定色盤，透明為其中一個選項。
- `MainWindow.xaml`：屬性頁新增「畫布」區塊，包含顯示格線、吸附至格線、畫布尺寸與畫布背景色盤。
- `MainWindow.xaml.cs`：新增畫布設定事件與同步邏輯；偏好設定移除格線、吸附、尺寸與背景控制項。
- `MainWindow.xaml.cs`：主題顏色改為選取後立即套用並儲存；偏好設定內容右側加入 16 DIP 間距，避免垂直捲軸覆蓋欄位。
- `Strings/zh-Hant/Resources.resw`：更新主題顏色、畫布尺寸、畫布背景等文案，新增畫布設定與背景色名稱，移除舊的頁面與透明開關字串。
- `Directory.Build.props`、`app.manifest`：版本 1.6.5。
- `CHANGELOG.md`、`RELEASE_NOTES.md`、`README.md`：同步版本與說明。

### 問題與解法
- 屬性頁與偏好設定都需要畫布設定 → 統一放到常駐的屬性頁「畫布」區塊，偏好設定只保留顯示模式、主題顏色、自動選取工具與檔案關聯。
- 偏好設定捲軸採覆蓋式顯示，會壓到右側欄位 → 在對話框內容加上右側 16 DIP 邊距。
- 主題顏色原本要按「套用」才生效 → 在 ComboBox 的 `SelectionChanged` 直接套用並寫入設定；取消不再還原已套用的主題顏色。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug`：0 警告、0 錯誤。
- `dotnet test MyDrawVector.slnx -c Debug`：Core 單元測試全數通過。
- `dotnet format MyDrawVector.slnx --verify-no-changes`：通過。
- 待實機確認：屬性頁四項設定即時生效、透明背景棋盤格、偏好設定捲軸間距、Undo/Redo 後屬性頁同步。

### 待辦
- 尚未實測 Windows 10 1809 與高 DPI 縮放。

---

## 2026-11-11 - v1.6.4

### 目標
精簡偏好設定版面，將「吸附至格線」集中到偏好設定，並新增可選的「點擊物件自動選擇選取工具」。

### 變更
- `Strings/zh-Hant/Resources.resw`：`BackgroundColorHeader` 改為「畫布背景顏色」；移除 `SnapToGridMenuItem.Text`，新增吸附、自動選取工具標題與共用開關文字。
- `MainWindow.xaml`：設定選單移除 `SnapToGridMenuItem`，吸附改由偏好設定控制。
- `MainWindow.xaml.cs`：移除 `SnapToGridToggle_Click` 與建構子中的選單狀態同步；偏好設定新增吸附與自動選取工具開關；畫布背景改為色塊按鈕加 Flyout 選色，並以 `ScrollViewer` 包住內容避免裁切。
- `MainWindow.xaml.cs`：點擊物件時若開啟自動選取工具設定且目前不是選取工具，先 `SetTool(EditorTool.Select)`。
- `Services/AppSettings.cs`：新增 `AutoSelectToolOnObjectClick`，預設 `false`。
- `Directory.Build.props`、`app.manifest`：版本 1.6.4。
- `CHANGELOG.md`、`RELEASE_NOTES.md`：同步版本與說明。

### 問題與解法
- 內嵌完整 `ColorPicker` 佔用大量高度，改為只顯示一列色塊按鈕，完整選色器放在 Flyout；透明背景時停用該按鈕並即時更新色塊。
- 偏好設定新增兩列後內容可能超出對話框，以 `MaxHeight 520` 的 `ScrollViewer` 包住，必要時可捲動。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug`：0 警告、0 錯誤。
- `dotnet test`：35 項 Core 單元測試全數通過。
- `dotnet format --verify-no-changes`：通過。
- 待實機確認：背景色 Flyout 選色、透明切換、吸附設定保存與自動切換工具行為。

### 待辦
- 尚未實測 Windows 10 1809、不同 DPI 與高對比模式。

---

## 2026-11-11 - v1.6.3

### 目標
修正新畫的物件一律出現在頁面左上角、拖曳時選取框移動但圖形不動的問題。

### 變更
- `MainWindow.xaml.cs`：`UpdateVisual()` 每次重繪都重新設定 `RenderTransformOrigin` 並指派新的 `MatrixTransform`，移除 v1.6.1 的「就地修改 `MatrixTransform.Matrix`」最佳化。
- `MainWindow.xaml.cs`：`ApplyView()` 同樣改回每次指派新的 `MatrixTransform`，並移除不再使用的 `_viewTransform` 欄位。

### 問題與解法
- 選取框由模型 `Transform` 計算、會正常移動，但物件視覺元素停在 `ObjectCanvas` 的 (0,0)，代表 `RenderTransform` 沒有被套用。
- v1.6.1 把「每次建立新的 `MatrixTransform`」改成「若已是 `MatrixTransform` 就只改 `Matrix`」。物件建立後第一次繪製可能取得預設的 identity 變形並走就地修改分支，WinUI 3 對原地修改 `MatrixTransform.Matrix` 不一定會即時重繪，導致物件一直停留在左上角、拖曳也不會更新。
- 改回每次指派新的 `MatrixTransform` 後，變形變更一定會觸發重新繪製。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug`：0 警告、0 錯誤。
- `dotnet test`：35 項 Core 單元測試全數通過。
- Debug 執行檔啟動並成功建立主視窗，日誌無例外。
- 待實機確認：新畫物件位置、拖曳、縮放、旋轉、平移縮放與 PNG 匯出。

### 待辦
- 若日後要再做變形相關快取，需先確認 `MatrixTransform` 的更新方式在目標 Windows App SDK 版本會即時重繪。

---

## 2026-11-11 - v1.6.2

### 目標
執行 P4 的 15–17：同步 manifest 版本、移除殘留的顯示模式設定欄位、補齊屬性面板與數值欄位的無障礙名稱。

### 變更
- `src/MyDrawVector/app.manifest`：`assemblyIdentity` 版本同步為 1.6.2.0。
- `Services/AppSettings.cs`：移除舊欄位 `IsDarkMode`。
- `Services/SettingsService.cs`：改以私有 `LegacySettings` 讀取舊設定檔的 `IsDarkMode`，只有 `ThemeMode` 為空時才轉換為 `Dark`。
- `MainWindow.xaml.cs`：偏好設定不再寫入 `IsDarkMode`。
- `Controls/NumericField.xaml`：加減按鈕新增 `AutomationProperties.Name` 與工具提示。
- `MainWindow.xaml`：旋轉、翻轉、物件順序與對齊按鈕新增無障礙名稱與工具提示。
- `Directory.Build.props`、`CHANGELOG.md`、`RELEASE_NOTES.md`：版本 1.6.2 與說明。

### 問題與解法
- 直接刪除欄位會讓舊設定的深色偏好消失 → 用私有 `LegacySettings` 讀取原始 JSON 後再交由 `Normalize` 驗證，新設定不再輸出該欄位。
- `AssemblyVersion` 依規範維持 1.6.0.0，只調整 `Version` 與 `FileVersion`。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug`：0 警告、0 錯誤。
- `dotnet test`：35 項 Core 單元測試全數通過。
- `dotnet format --verify-no-changes`：通過。
- Debug 執行檔啟動後主視窗建立並保持執行，日誌無例外。

### 待辦
- 尚未以 Narrator 實機確認屬性面板按鈕的朗讀順序。

---

## 2026-11-11 - v1.6.1

### 目標
依「改動小、效益高」清單執行 P1 效能與 P2 穩定度項目，且不改變既有操作行為。

### 變更
- `Core/History/UndoManager.cs`：新增 `ShouldPushCoalesced`；`PushCoalesced` 改用它。
- `Core/Serialization/DocumentSerializer.cs`：新增 `SerializeCompact`（不縮排，其餘選項相同）。
- `ViewModels/EditorViewModel.cs`：`PushUndo` 先判斷 coalesce，未通過就不序列化；復原／重做與手勢快照改用精簡序列化；新增 `ChangeVersion` 供自動儲存去重，於 `MarkDirty`、`Undo`、`Redo` 遞增。
- `Core/Serialization/DocumentPackageService.cs`：`assets/*` 改用 `CompressionLevel.NoCompression`。
- `Helpers/ColorHelper.cs`：`Brush` 以正規化 hex 快取 `SolidColorBrush`。
- `MainWindow.xaml`／`.xaml.cs`：新增 `PointerCaptureLost` 處理；`UpdateGrid` 依尺寸與顯示狀態快取；`ObjectCanvas`／`PreviewCanvas` 裁切與 `SceneRoot` 檢視變形改為重用實例；物件 `RenderTransform` 重用；浮動工具列尺寸依多選狀態與 `RasterizationScale` 快取。
- `App.xaml.cs`：`--register`／`--unregister` 完成後直接 `Exit()`。
- `MainWindow.xaml.cs`：自動儲存比對 `ChangeVersion`，內容未變不重寫。
- `Services/FileLoggerProvider.cs`：`app.log` 超過 1 MB 時輪替為 `app.log.1`。
- `Directory.Build.props`：版本 1.6.1；`app.manifest` 的 `assemblyIdentity` 同步為 1.6.1.0。
- `CHANGELOG.md`、`RELEASE_NOTES.md`：同步版本與更新內容。

### 問題與解法
- 快取 `SolidColorBrush` 後不可再改色 → 目前程式只讀取共用筆刷，主題色仍走 `SetBrushColor` 的資源筆刷，未受影響。
- 匯出 PNG 會暫時替換 `SceneRoot.RenderTransform` → 重用 `MatrixTransform` 後仍以參考還原，行為不變。
- `ReleasePointerCapture` 可能再觸發 `PointerCaptureLost` → 以 `_dragMode == None` 提早返回，避免重複取消。
- `AssemblyVersion` 依規範僅主次版變更時調整，本次維持 1.6.0.0。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug`：0 警告、0 錯誤。
- `dotnet test`：35 項 Core 單元測試全數通過（含新增的 coalesce 判斷與精簡序列化測試）。
- `dotnet format --verify-no-changes`：通過。
- 待實機確認：連續拖曳透明度、含圖片存檔、Alt+Tab 中斷拖曳、`--unregister` 不開視窗。

### 待辦
- 尚未實測 Windows 10 1809、不同 DPI 與高對比模式。
- 屬性面板樣式調整仍會整份重繪，後續可改為只更新選取物件。

---

## 2026-11-11 - v1.6.0

### 目標
依「改動小、效益高」清單，一次補齊介面回饋、編輯手感與檔案流程的一致性。

### 變更
- `MainWindow.xaml`：圖形樣式新增「啟用填色／啟用邊框」開關；邊框 Flyout 加入與填色共用的 20 色盤；新增說明選單、`DeleteToast` 復原提示、狀態列頁面／格線／吸附與自動儲存文字、屬性面板收合鈕、浮動選取工具列（`ActionCanvas`）、物件與圖層列內嵌顯示／鎖定按鈕、`CheckerCanvas` 透明棋盤格、`InspectorColumn`。
- `MainWindow.xaml.cs`：`BuildPresetColorGrid` 共用色盤建立；`ApplyStrokeColor` 與填色流程對齊；Alt 暫時關閉吸附（`IsSnappingEnabled`）；復原提示計時器；`GetPageDisplayName`；`CycleSelection`；Ctrl+Shift+Z；文字對話框取消不再建立物件；面板收合；浮動工具列定位與動作；列內按鈕事件；頁面背景與透明棋盤格；匯出時隱藏棋盤格。
- `ViewModels/EditorViewModel.cs`：`Undo`／`Redo` 以物件 ID 與圖層 ID 還原選取。
- `MainWindow.xaml.cs`：開啟、儲存、匯出改用 `Microsoft.Windows.Storage.Pickers`，匯出改以記憶體編碼後寫檔。
- `Directory.Build.props`：版本 1.6.0。
- `README.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`、`Strings/zh-Hant/Resources.resw`：同步功能、快捷鍵與新文案。

### 問題與解法
- `ColorPicker` 沒有 `Header` 屬性 → 以 `TextBlock` 加 `ColorPicker` 組成背景顏色區塊。
- `CommandButtonStyle` 的 `TargetType` 是 `Button`，直接套在 `ToggleButton` 會造成啟動時 `XamlParseException`（TargetType 不符）→ 新增 `CommandToggleButtonStyle`。
- 透明背景若只改 alpha，畫布上不易辨識 → 在頁面與格線之間加棋盤格層，僅在背景 alpha 為 0 時建立與顯示；匯出時一併隱藏。
- 浮動工具列若放在選取框上方會蓋住旋轉手柄 → 預設放在選取框下方 12 DIP，空間不足才移到上方。
- Alt 會觸發選單列 → 在 `RootGrid_KeyDown` 攔截 `VirtualKey.Menu`，同時以鍵盤狀態判斷暫時關閉吸附。
- 新版 `FileSavePicker` 需以 `_appWindow.Id` 建構，且 `PickSaveFileAsync` 回傳 `PickFileResult`；匯出因此改為 `InMemoryRandomAccessStream` 編碼後 `File.WriteAllBytesAsync`。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug`：0 警告、0 錯誤。
- `dotnet test`：33 項 Core 單元測試全數通過。
- `dotnet format --verify-no-changes`：通過。
- Debug 執行檔啟動後主視窗成功建立並保持執行，日誌無例外。
- 待實機確認：Alt 拖曳不吸附且不彈選單、浮動工具列不遮手柄、透明頁面匯出、新版存檔／匯出對話框。

### 待辦
- 尚未實測 Windows 10 1809、不同 DPI 與高對比模式。
- 浮動工具列在極小縮放或物件貼齊畫布邊緣時的避讓仍需實機微調。
- 頁面背景變更目前記在文件的復原歷史中，尚未提供「套用至所有新文件」的預設值。

---

## 2026-11-11 - v1.5.3

### 目標
調整屬性面板順序，讓圖形樣式優先顯示，並把透明度集中在圖形樣式下方。

### 變更
- `MainWindow.xaml`：`ShapeSection` 移到 `NoSelectionText` 之後；透明度獨立為 `OpacitySection`，置於圖形樣式之後、旋轉與翻轉之前。
- `MainWindow.xaml.cs`：`UpdateInspector()` 依選取狀態切換 `OpacitySection` 可見性；文字與圖片仍可使用透明度。
- 版本升為 1.5.3，同步更新 CHANGELOG、RELEASE_NOTES。

### 問題與解法
- 透明度原本位於 `ObjectSection`，直接搬移會讓文字與圖片失去控制；改為獨立區塊，只隨選取狀態顯示。

### 待辦
- 尚未實機確認屬性面板捲動位置在區塊順序調整後仍保持穩定。

---

## 2026-11-11 - v1.5.2

### 目標
避免在非圓角矩形物件顯示無效的「圓角」屬性。

### 變更
- `MainWindow.xaml.cs`：`UpdateInspector()` 依 `ShapeKind.RoundedRectangle` 切換 `CornerRadiusBox` 的可見性。
- 版本升為 1.5.2，同步更新 CHANGELOG、RELEASE_NOTES。

### 問題與解法
- 圓角欄位原本位於共用的 `ShapeSection`，所有圖形都會顯示；改在選取圖形時個別設定 `Visibility`，不影響其他圖形樣式欄位。

### 待辦
- 尚未實機確認切換不同圖形時欄位隱藏不造成屬性面板跳動。

---

## 2026-11-11 - v1.5.1

### 目標
精簡屬性面板，移除中心 X／中心 Y／寬度／高度與旋轉角度數值欄位，減少重複的數值操作入口。

### 變更
- `MainWindow.xaml`：移除 `CenterXBox`、`CenterYBox`、`WidthBox`、`HeightBox` 與 `RotationBox`；屬性區塊標題改為「旋轉與翻轉」，保留 0／90／180／270 預設角度與翻轉按鈕。
- `MainWindow.xaml.cs`：`UpdateInspector()` 不再更新上述欄位；刪除 `TransformBox_ValueChanged` 與 `RotationBox_ValueChanged`。
- `README.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`：同步更新功能描述與版本 1.5.1。

### 問題與解法
- 移除 XAML 控制項後，程式碼仍參照會造成編譯錯誤；一併移除事件處理與 `UpdateInspector()` 中的賦值，並確認 `MinObjectSize` 仍由畫布縮放流程使用。

### 驗證
- Debug 建置通過，Core 測試不受影響。
- 位置與尺寸仍可透過畫布拖曳與縮放控制點調整；旋轉仍可使用手柄、Shift 吸附與預設角度按鈕。

### 待辦
- 尚未實機確認屬性面板在窄視窗與不同 DPI 下的間距。

---

## 2026-11-11 - v1.5.0

### 目標
讓任何工具都能直接選取與變形物件而不自動切換工具，並改善圖片對話框開啟緩慢、偶爾不出現的問題；同時在工具列頂端加入復原與重做按鈕。

### 變更
- `MainWindow.xaml.cs`：移除點選物件時自動 `SetTool(EditorTool.Select)`；控制點命中判斷不再要求目前工具為「選取」，任何工具下都能拖曳、縮放、旋轉已選取物件。
- `MainWindow.xaml.cs`：圖片工具改為動作按鈕，點擊或按 `I` 立即開啟對話框，選取後插入頁面中央；移除畫布上的圖片工具點擊流程。
- `MainWindow.xaml.cs`：圖片選檔改用 `Microsoft.Windows.Storage.Pickers.FileOpenPicker`，以 `_appWindow.Id` 指定擁有者視窗，並以 `_isPickingImage` 防止重複開啟；`RequestInsertImage` 以 `DispatcherQueue` 延後開啟並先啟用視窗。
- `MainWindow.xaml.cs`：替換圖片同步改用新版 Picker 與 `File.ReadAllBytesAsync`。
- `MainWindow.xaml`：新增 `ToolCommandButtonStyle`，在左側工具列最上方加入復原／重做按鈕與分隔線；圖片工具提示改為「插入圖片 (I)」。
- `Strings/zh-Hant/Resources.resw`：新增復原／重做按鈕資源並更新圖片工具文字。
- 版本提升至 1.5.0，同步更新 README、CHANGELOG、RELEASE_NOTES。

### 問題與解法
- 舊版 `Windows.Storage.Pickers.FileOpenPicker` 搭配 `InitializeWithWindow` 在免安裝 WinUI 3 應用中首次開啟較慢，連續點擊時也可能同時建立多個對話框。改用 Windows App SDK 新版 Picker，集中以單一旗標防止重入，並避開圖片媒體櫃解析。
- 點選物件自動切換工具會中斷目前的繪製流程。移除切換後，控制點需在任何工具下都能命中，否則無法縮放，因此同步放寬控制點判斷條件。
- 圖片工具原本是持續模式，需再點畫布才會開對話框。改為立即執行的動作按鈕，並在取消後維持原本工具。

### 驗證
- Debug 全方案建置成功，0 個警告、0 個錯誤。
- 33 項 Core 單元測試全數通過。
- `dotnet format --verify-no-changes` 通過，僅顯示既有工作區載入警告。
- 待實際畫面驗證新版圖片對話框、重複點擊防護與工具列復原／重做按鈕狀態。

### 待辦
- 尚未實測 Windows 10 1809、不同 DPI 與 Windows 高對比模式。
- 若特定環境的新版 Picker 仍有問題，備案為改用 Win32 `IFileOpenDialog`。
- 開啟／儲存／匯出對話框仍使用舊版 WinRT Picker，本次僅調整圖片相關流程。

---

## 2026-11-11 - v1.4.0

### 目標
統一左側工具列按鈕的前景與背景狀態變化，並讓「更多圖形」按鈕顯示目前選取的子選單工具圖示。

### 變更
- `MainWindow.xaml`：更多圖形改為 ToggleButton 並沿用 `ToolButtonStyle`，子選單改用 `FlyoutBase.AttachedFlyout`；集中定義 8 個子選單圖示 DataTemplate，移除未使用的 `ToolFlyoutButtonStyle`。
- `MainWindow.xaml.cs`：新增工具按鈕映射，`SetTool` 統一更新 `IsChecked`；更多圖形依目前工具載入對應圖示，並處理 attached flyout 的開啟與關閉。
- `Controls/ToolPathIcon.xaml`／`.xaml.cs`：改為載入時將 `Path.Stroke` 繫結到最近 `ContentPresenter.Foreground`，修正選取狀態及子選單重開後圖示顏色未更新的問題。
- 版本提升至 1.4.0，同步更新 CHANGELOG 與 RELEASE_NOTES。

### 問題與解法
- 直接共用子按鈕的 Geometry 會讓第二個 Path 設定 Data 時擲出 E_INVALIDARG；改用 `DataTemplate.LoadContent()` 產生獨立圖示與 Geometry。
- `x:Bind Foreground` 在子選單關閉重開後不會重新套用，且 UserControl 的繼承值變化不會觸發變更通知；改在 Loaded 時把 `Path.Stroke` 繫結到 `ContentPresenter.Foreground`。
- 更多圖形改為 ToggleButton 後沒有 `Flyout` 屬性，改用 `FlyoutBase.AttachedFlyout` 搭配 `ShowAttachedFlyout`；Click 內先還原自動翻轉的 `IsChecked`。

### 驗證
- Debug 全方案建置成功，0 個警告、0 個錯誤。
- 33 項 Core 單元測試全數通過；格式驗證通過，僅顯示既有工作區載入警告。
- 實際畫面驗證：選取工具時為 accent 背景與白色圖示，切換後恢復深色圖示；更多圖形選三角形後顯示白色三角形並標示選取，重開子選單仍為白色；改選文字後恢復預設圖示。

### 待辦
- 尚未實測 Windows 10 1809、不同 DPI 與 Windows 高對比模式。

---

## 2026-11-10 - v1.3.2

### 目標
統一所有工具圖示的狀態變色方式，並依指定樣式調整更多圖形的子選單記號。

### 變更
- `MainWindow.xaml`：更多圖形角標改用 `TextFillColorSecondaryBrush` 灰色 Polygon，座標為 `0,5 5,5 5,0`，距右、下邊緣各 4 DIP。
- 確認 `FontIcon`、`SymbolIcon` 與 `ToolPathIcon` 均由 ToggleButton 的 ContentPresenter 繼承狀態前景色。
- 版本提升至 1.3.2，並同步更新 manifest、CHANGELOG 與 RELEASE_NOTES。

### 問題與解法
- 子選單角標是固定提示而非工具選取圖示，不應跟隨按鈕前景變成黑色；改用主題感知的次要文字灰色。

### 驗證
- Debug 全方案建置成功，0 個警告、0 個錯誤。
- 33 項 Core 單元測試全數通過；格式驗證通過，僅顯示既有工作區載入警告。
- PrintWindow 實際畫面確認角標為按鈕右下角的灰色 5×5 Polygon，選取箭頭仍使用高對比狀態前景色。
- Debug 執行檔成功建立主視窗並完成畫面擷取，未發生啟動例外。

### 待辦
- 尚未實測 Windows 10 1809、不同 DPI 與 Windows 高對比模式。

---

## 2026-11-10 - v1.3.1

### 目標
修正介面主題色未實際生效、工具選取圖示對比不足，以及更多圖形角標位置不正確。

### 變更
- `MainWindow.xaml.cs`：改為直接更新 `XamlControlsResources` 的 Light 與 Default 既有強調色 Brush，讓已建立控制項與 Flyout 即時同步。
- `Controls/ToolPathIcon.xaml`／`.xaml.cs`：新增可繼承 ToggleButton 原生狀態前景色的共用描邊工具圖示。
- `MainWindow.xaml`：所有 Path 工具改用共用圖示控制項；更多圖形按鈕改為 44×44 Overlay Grid，角標定位至外框右下角。
- 版本提升至 1.3.1，並同步更新 manifest、README、CHANGELOG 與 RELEASE_NOTES。

### 問題與解法
- WinUI 的 `ToggleButtonBackgroundChecked` 等別名以 `StaticResource` 持有原始藍色 Brush；不再替換字典項目，改為保留物件並更新 `Color`。
- ToggleButton 選取狀態改變的是模板內 `ContentPresenter.Foreground`，不是外層 Button.Foreground；描邊圖示改為繼承 ContentPresenter 前景色。
- 原角標相對扣除 Padding 後的 24×24 內容區定位；改由按鈕外層 44×44 Grid 負責 Overlay 定位。

### 驗證
- Debug 全方案建置成功，0 個警告、0 個錯誤。
- 33 項 Core 單元測試全數通過；格式驗證通過，僅顯示既有工作區載入警告。
- PrintWindow 實際畫面確認護眼黃主題取代系統藍色、選取箭頭為白色，且角標位於按鈕外框右下角。
- Debug 執行檔成功建立主視窗並保持執行，未於啟動階段退出。

### 待辦
- 尚未實測 Windows 10 1809、不同 DPI 與 Windows 高對比模式。

---

## 2026-11-10 - v1.3.0

### 目標
新增可持久化的介面主題色與快速填色色盤，並改善更多圖形入口及圖形屬性的操作順序。

### 變更
- `Services/AppThemePalette.cs`／`AppSettings.cs`／`SettingsService.cs`：新增 10 種介面主題色目錄、設定欄位與未知值回退。
- `MainWindow.xaml.cs`：套用 WinUI 強調色資源；偏好設定新增主題色選擇；填色流程加入 20 色快速選擇並沿用 Undo 與新圖形預設。
- `Helpers/FillColorPalette.cs`：集中定義 20 個填色選項與繁體中文資源鍵。
- `MainWindow.xaml`：更多圖形按鈕新增子選單三角記號；填色 Flyout 加入 5×4 色盤；線條樣式移到填色與邊框下方。
- `Strings/zh-Hant/Resources.resw`：新增主題色與預設填色色名。
- 版本提升至 1.3.0，並同步更新 manifest、README、CHANGELOG 與 RELEASE_NOTES。

### 問題與解法
- 主題色需與顯示模式及畫作顏色分離；使用獨立設定 ID，僅覆寫 WinUI 語意強調色資源，高對比模式回退系統資源。
- ColorPicker 初始化具有事件保護期，快速色票不能只依賴 `ColorChanged`；改由色票與 ColorPicker 共用填色套用方法，程式化同步 Picker 時抑制重入。

### 驗證
- Debug 全方案建置成功，0 個警告、0 個錯誤。
- 33 項 Core 單元測試全數通過；格式驗證通過，僅顯示既有工作區載入警告。
- Debug 執行檔成功啟動並保持執行，主題資源與 20 色控制項初始化未造成啟動例外。

### 待辦
- 尚未實測 Windows 10 1809、不同 DPI、所有主題色對比與高對比模式。

---

## 2026-11-10 - v1.2.4

### 目標
改善工具圖示邊界、畫紙外繪圖、滾輪操作與淺色模式工作區的顯示體驗。

### 變更
- `MainWindow.xaml`：選取工具改為空心箭頭，幾何工具圖案縮小至保留安全邊界；工作區改用淺色、深色與高對比主題資源。
- `MainWindow.xaml.cs`：預覽層依縮放後畫紙範圍裁切；滑鼠滾輪統一改為以游標位置為中心縮放。
- `README.md`：更新滾輪與畫紙裁切操作說明。
- 版本提升至 1.2.4，並同步更新 manifest、CHANGELOG 與 RELEASE_NOTES。

### 問題與解法
- 最終物件原本已有畫紙裁切，但繪製預覽位於獨立螢幕座標層；在每次套用縮放與平移時同步更新預覽層裁切矩形，且保留選取控制點不裁切。
- 工具圖案填滿 24 DIP 內容區，描邊外擴後可能越界；維持 44 DIP 按鈕命中區並將圖案縮小至 20 DIP。
- WinUI XAML 不接受在隱含的 `Grid.Resources` 字典直接宣告 `ThemeDictionaries`，造成 WMC9997；改用明確的 `ResourceDictionary` 包裝主題字典與既有資源。

### 驗證
- Debug 全方案建置成功，0 個警告、0 個錯誤。
- 33 項 Core 單元測試全數通過；格式驗證通過，僅顯示既有工作區載入警告。
- Debug 執行檔成功啟動，主視窗保持執行且未於啟動階段結束。

### 待辦
- 尚未實測不同 DPI、高對比模式與觸控板滾輪。

---

## 2026-11-10 - v1.2.3

### 目標
改善工具列圖示辨識度與選單快捷鍵可發現性，並修正畫紙平移後超出工作區、覆蓋介面的問題。

### 變更
- `MainWindow.xaml`：幾何工具改用主題感知的空心描邊圖示；重畫選取箭頭、直線、四向平移與規則多邊形。
- `MainWindow.xaml`／`.xaml.cs`：編輯選單新增剪下、複製、貼上、再製、刪除、全選及快捷鍵文字；新增 `Ctrl+Shift+S` 另存。
- `CanvasHostControl.cs`：依工作區實際尺寸裁切畫紙、陰影、格線、預覽和選取控制點。
- `MainWindow.xaml.cs`：集中限制平移範圍，四邊至少保留 40 DIP 畫紙可見。
- `Strings/zh-Hant/Resources.resw`：新增完整編輯選單文案。
- 版本提升至 1.2.3，並同步更新 manifest、README、CHANGELOG 與 RELEASE_NOTES。

### 問題與解法
- `PathIcon` 只會填色，導致封閉幾何實心、開放直線不可見；改用具有 `Stroke` 的 `Path`。
- Grid 版面不會可靠裁切變形後的子元素；由 `CanvasHostControl.ArrangeOverride` 套用 `RectangleGeometry` 裁切。
- `_pan` 原本沒有邊界；在每次 `ApplyView` 前依縮放後畫紙尺寸與工作區大小統一限制。

### 驗證
- Debug 全方案建置成功，0 個警告、0 個錯誤。
- 33 項 Core 單元測試全數通過；格式驗證通過，僅顯示既有工作區載入警告。
- Debug 執行檔成功建立並啟用主視窗，啟動日誌未記錄例外。

### 待辦
- 尚未實測 Windows 10 1809、不同 DPI 與高對比模式。

---

## 2026-11-10 - v1.2.2

### 目標
更新應用程式圖示，並以系統選單、集中式偏好設定及兩層圖示工具列簡化主要操作介面。

### 變更
- `Assets/app.ico`／`app-icon-source.svg`／`tools/make-icon.ps1`：圖示改為藍色圓角底搭配黃色星星，重新產生 16、24、32、48、64、128、256 px 圖框。
- `MainWindow.xaml`：移除頂部文字快捷列，新增檔案／編輯／設定 MenuBar；未儲存提示移至狀態列。
- `MainWindow.xaml`／`.xaml.cs`：左側改為純圖示工具，常用工具留在第一層，其他圖形收進 Flyout，並保留 Tooltip 與無障礙名稱。
- `MainWindow.xaml.cs`：新增偏好設定對話框，集中顯示模式、格線、頁面尺寸及檔案關聯。
- `Strings/zh-Hant/Resources.resw`：集中新增選單、工具 Tooltip、無障礙名稱與偏好設定文案。
- 版本提升至 1.2.2，並同步更新 manifest、README、CHANGELOG 與 RELEASE_NOTES。

### 問題與解法
- WinUI 程式化建立粗體文字時誤用 `Windows.UI.Text.FontWeights`，改用 `Microsoft.UI.Text.FontWeights` 後 x64 Debug 建置成功。
- 方案不提供 `Debug|x64` 組態；改用方案預設 Debug 組態驗證全方案，另以應用程式專案的 x64 平台確認目標架構。

### 驗證
- Debug 全方案建置成功，0 個警告、0 個錯誤。
- 33 項 Core 單元測試全數通過；格式驗證通過，僅顯示既有工作區載入警告。
- 啟動 Debug 執行檔後主視窗成功建立並啟用，啟動日誌未記錄例外。

### 待辦
- 尚未實測 Windows 10 1809、不同 DPI 與高對比模式下的圖示及 Flyout。

---

## 2026-11-10 - v1.2.1

### 目標
修正 v1.2.0 執行檔啟動後立即崩潰，以及單一實例重複啟動的競態與背景程序問題。

### 變更
- `MainWindow.xaml`／`.xaml.cs`：縮放 Slider 的範圍與初始值改由建構式依序設定，避免 XAML 載入時超出預設範圍。
- `App.xaml.cs`：在建立檔案記錄器前先完成單一實例判定，避免第二實例鎖住 `app.log`。
- 第二實例完成啟動轉交後呼叫 `Exit()`，不再留下無視窗背景程序。
- 主視窗建立與啟用加入階段記錄，啟動例外會完整寫入日誌。

### 問題與解法
- Windows 事件記錄僅顯示 `CoreMessagingXP.dll`、`0xc000027b`／`0x802b000a`，無法直接定位；在主視窗建立外層加入記錄後，確認是 Slider 的 `Minimum`／`Value` 在 `Maximum` 前套用而引發 `XamlParseException`。
- `OnLaunched` 直接返回不會停止第二個 WinUI 訊息迴圈；轉交完成後明確呼叫 `Exit()`。

### 驗證
- Debug 建置成功，0 個警告、0 個錯誤；33 項單元測試全數通過；格式檢查無差異。
- Debug 與 Release 主視窗均成功顯示並保持回應。
- 快速啟動第二實例後，主實例保持運作，第二實例以代碼 0 結束。
- 已重新產生 `publish/MyDrawVector-portable-win-x64`，執行檔版本為 1.2.1，且包含 PRI 與完整相依檔案。

### 待辦
- 尚未實測 Windows 10 1809 與不同 DPI。

---

## 2026-11-10 - v1.2.0

### 目標
依「改動小、效益高」清單，補齊高優先與次優先的介面與功能優化。

### 變更
- `UndoManager` 新增 `PushCoalesced`；透明度、色盤、粗細、圓角、字級、行距、X/Y/W/H、旋轉連續調整合併為一筆復原。
- 新圖形沿用上次填色、邊框、粗細、虛線與圓角，並存入 `AppSettings`。
- 開啟鈕改為 `SplitButton`，提供最近檔案選單與清除紀錄。
- 新增圓角矩形、箭頭、菱形、五邊形、六邊形、對話框工具。
- 新增工具快捷鍵與縮放列百分比、放大／縮小、100%、適合視窗與 Ctrl+0／Ctrl+1／Ctrl++／Ctrl+-。
- 設定 `OverlappedPresenter.PreferredMinimumWidth/Height`，避免版面擠壞。
- 工具按鈕與圖示按鈕補 `AutomationProperties.Name`。
- 新增 20 DIP 格線顯示與吸附，每 100 DIP 為較深主格線；吸附容差 8 DIP，套用於移動、縮放、建立圖形；抽出 `GridSnap` 並加測試。
- 顯示模式改為跟隨系統／淺色／深色，`UISettings.ColorValuesChanged` 即時同步。
- 物件與圖層清單改為 `ItemTemplate`，顯示隱藏與鎖定圖示。
- 設定新增 A4 橫／直、A3、Letter、16:9 頁面尺寸；PNG 匯出可選 1×／2×／3×。
- 新增 `AutosaveService`，每 60 秒自動儲存並於啟動時詢問恢復。
- App 改用 `AppInstance` 單一實例，重複啟動會帶回原視窗並轉交檔案。
- 版本提升至 1.2.0；更新 README、CHANGELOG、RELEASE_NOTES。

### 問題與解法
- `SplitButton.Click` 的委派是 `TypedEventHandler<SplitButton, SplitButtonClickEventArgs>`，事件處理常式需改用此簽名。
- `Windows.System.VirtualKey` 沒有 `OemPlus`／`OemMinus`／`Alt`，改用 `(VirtualKey)0xBB`、`(VirtualKey)0xBD` 與 `VirtualKey.Menu`。
- `RELEASE_NOTES.md` 舊檔為非 UTF-8 且部分字元已毀損，改寫為乾淨的 UTF-8 版本，舊版資訊保留在 `CHANGELOG.md`。

### 待辦／已知限制
- 尚未實作群組／解群組、智慧參考線、範本、均分、物件跨圖層搬移。
- 尚未實作 SVG／JPG／PDF 匯出與列印。
- 尚未實測不同 DPI 與 Windows 10 1809 的格線、自動儲存與單一實例行為。
- 新字串仍為硬編碼，尚未全面移到 `.resw`。

---

## 2026-11-10 - v1.1.2

### 目標
改為非 self-contained 發行；NumericField +/− 支援按住連續；修正屬性面板跳動與旋轉欄位裁切。

### 變更
- NumericField 的 +/− 改為 `RepeatButton`（Delay 400ms、Interval 80ms）。
- 發行改為 `--self-contained false`、`WindowsAppSDKSelfContained=false`。
- 屬性面板新增 `InspectorScroll`，更新後還原垂直位置；避免不必要重綁 ItemsSource。
- 旋轉角度 NumericField 改為整列，0／90／180／270 按鈕移到下方。
- 版本提升至 1.1.2，更新文件與使用說明。

### 問題與解法
- NumericField XAML 的 RepeatButton 需加入 `xmlns:primitives`。
- 非 self-contained 版需目標電腦安裝 .NET 10 Desktop Runtime 與 Windows App SDK 2.6.5 Runtime。

### 待辦／已知限制
- 尚未實作群組、吸附、範本、均分、物件跨圖層搬移。
- 依需求不建立 zip。

---

## 2026-11-10 - v1.1.1

### 目標
修正數值欄位 hover 誤觸發、Ctrl+Z tooltip、旋轉欄位自動變 0，並調整圖層面板順序。

### 變更
- 新增 `Controls/NumericField`：TextBox + 自訂 −／+ 按鈕。
- 所有 NumberBox 改為 NumericField；移除 NumberBoxInputBehavior。
- NumericField 空白時以目前模型值為基準，失焦還原目前值，不自動變 0。
- 移除 XAML KeyboardAccelerator；快捷鍵統一在 RootGrid_KeyDown 處理。
- 「目前圖層物件」區塊移到「圖層」區塊上方。
- 版本提升至 1.1.1。

### 問題與解法
- XAML 事件委派要求的簽名是 `EventHandler<T>`，處理常式第一個參數需為 `object`，改為 `object? sender`。
- NumericField 上下鈕只用 Click，避免 hover 誤觸發。

### 待辦／已知限制
- 尚未實作群組、吸附、範本、均分、物件跨圖層搬移。
- 依需求不建立 zip。

---

## 2026-11-10 - v1.1.0

### 目標
修正旋轉欄位、深色模式、圖形預覽與工具點擊行為，並加入真正的圖層系統。

### 變更
- Core：新增 `Layer`、`SceneObject.LayerId`、`Document.Layers`、圖層排序與舊檔遷移。
- 圖層 UI：新增／改名／刪除／上下移動／顯示隱藏／鎖定、目前圖層物件清單。
- 刪除圖層：空圖層直接刪除；有物件先確認；最後一層不可刪除。
- 新增物件、貼上、再製一律放入目前圖層。
- 點擊物件優先選取並切換到「選取」；建立物件後保持目前工具。
- 圖形拖曳即時預覽矩形、橢圓、直線、三角形、星形。
- 右鍵拖曳平移與移動游標。
- 數值欄位空值保護；旋轉欄位清空後上下鈕可用。
- 深色模式：根背景、提示、顏色按鈕色塊、對話框主題。
- 版本提升至 1.1.0。

### 問題與解法
- PowerShell `Get-Content` 預設編碼曾造成 `MainWindow.xaml.cs` 中文字串損壞 → 以 Python UTF-8 讀寫修復，之後只用 UTF-8 明確編碼處理原始碼。
- XAML 自訂 `CanvasHostControl` 初次編譯失敗 → 先修正 `EllipseGeometry` 的 `Rect` 錯誤後恢復。
- 舊檔沒有 `layers` → `Document.EnsureLayers()` 自動建立預設圖層並分配物件。

### 待辦／已知限制
- 尚未實作群組、吸附、範本、均分、物件跨圖層搬移。
- PNG 匯出與關閉提示仍需手動實測。
- 依需求不建立 zip，只發行資料夾。

---

## 2026-11-10 - v1.0.0

### 目標
建立可執行的免安裝 Windows 向量繪圖程式，涵蓋幾何圖形、文字、圖片、自由筆畫、選取變形、存檔與檔案關聯。

### 變更
- 建立 `MyDrawVector.slnx`、Core 類別庫、WinUI 3 應用程式與 xUnit 測試專案。
- `MyDrawVector.Core`：物件模型、`Matrix3x2` 變形、命中測試、JSON 序列化、`.mydraw` ZIP 格式、復原歷史。
- `MyDrawVector`：畫布渲染、縮放平移、圖形／文字／圖片／鉛筆工具、8 控制點縮放、旋轉、翻轉、屬性面板、圖層清單（顯示／隱藏、鎖定、改名）、對齊、剪貼簿、PNG 匯出。
- `FileAssociationService`：HKCU `.mydraw` 關聯建立／更新／移除，啟動時自動檢查，支援 `--register`／`--unregister`。
- 免安裝 self-contained x64 發行與多尺寸 `app.ico`。
- 新增 `README.md`、`CHANGELOG.md`、`RELEASE_NOTES.md`，並更新 `docs/FEATURE_SPEC.md` 為免安裝版本。

### 問題與解法
- WinUI 3 的 `Window` 沒有 `Resources` → 將資源移到根 `Grid.Resources`。
- `ColorHelper` 與 `Microsoft.UI.ColorHelper` 名稱衝突 → 使用 using alias。
- `FontWeights` 不在 `Windows.UI.Text` → 改用 `Microsoft.UI.Text.FontWeights`。
- MVVM Toolkit `[ObservableProperty]` 在 WinUI 產生 MVVMTK0045 → 改用手寫 `SetProperty` 屬性。
- Release 發行缺少 `MyDrawVector.pri`，啟動時 XAML 解析失敗 → 在 csproj 加入 `CopyAppPriToPublish` target，將 PRI 複製到發行資料夾。
- `--unregister` 後自動註冊又把它加回來 → 增加 `requestedUnregister` 判斷；並清除 `.mydraw` 的 Content Type 與空 OpenFileProgids。

### 驗證
- `dotnet build MyDrawVector.slnx -c Debug` 成功。
- `dotnet test`：12 個測試通過。
- 免安裝版啟動成功；自動建立檔案關聯；雙擊範例 `.mydraw` 可開啟並顯示圖層與狀態。
- 以程式建立矩形後標題出現未儲存標記。
- 儲存既有 `.mydraw` 後檔案內容更新。

### 待辦／已知限制
- 尚未實作群組／解群組、吸附與智慧參考線、範本庫、圖層顯示／鎖定／改名、均分。
- 尚未實作 JPG／SVG／PDF 匯出與列印。
- 尚未實作自動儲存／當機恢復、最近檔案 UI、單一實例。
- 文字編輯目前使用對話框；字串尚未全部移到 `.resw`。
- PNG 匯出與關閉前儲存提示尚未完成自動化實測。

---

## 2026-11-10 - v0.1.0（規劃）

### 目標
建立 v1.0.0 功能規格，作為後續實作與驗收依據。

### 變更
- 新增 `docs/FEATURE_SPEC.md`：功能範圍、介面、架構、物件模型、檔案格式、里程碑、測試與風險。

### 問題與解法
- 無。

### 待辦／已知限制
- 待確認原生格式、PDF／列印範圍、App 名稱與發行方式。
- 尚未建立 Solution 與程式碼。
