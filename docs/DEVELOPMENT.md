# MyDrawVector 開發與發行指南

- 文件版本：1.0
- 對應軟體版本：1.6.12
- 適用對象：開發者、貢獻者、發行維護者

---

## 1. 開發環境

| 項目 | 需求 |
| --- | --- |
| 作業系統 | Windows 10 1809（17763）以上 |
| SDK | .NET 10 SDK |
| IDE | Visual Studio 2026 或支援 WinUI 3 的版本（選用） |
| 執行環境 | .NET 10 Desktop Runtime、Windows App SDK 2.6.5 Runtime |
| 架構 | x64；ARM64 發行需 ARM64 工具鏈 |

確認環境：

```powershell
dotnet --version
dotnet workload list
```

本專案使用 Central Package Management，套件版本集中於 `Directory.Packages.props`，請勿在個別 `*.csproj` 寫死版本。

---

## 2. 取得原始碼

```powershell
git clone https://github.com/mark216tw/win-mydrawvector-01.git
cd win-mydrawvector-01
```

或直接使用既有工作目錄。

---

## 3. 建置

```powershell
dotnet build MyDrawVector.slnx -c Debug
```

- 開發與測試一律使用 `Debug`。
- 不要自動建置或發佈 `Release`，除非要建立發行版本。
- 啟用 `Nullable`、`ImplicitUsings` 與 `TreatWarningsAsErrors`，請勿關閉警告。

---

## 4. 測試

```powershell
dotnet test MyDrawVector.slnx -c Debug
```

測試專案：`tests/MyDrawVector.Core.Tests`。

目前涵蓋：

- 幾何變形與命中測試
- 圖層正規化與舊版遷移
- JSON 序列化與反序列化
- `.mydraw` ZIP 儲存與載入
- 復原歷史與合併邏輯

新增功能時請同步加入或更新測試。

---

## 5. 格式驗證

```powershell
dotnet format MyDrawVector.slnx --verify-no-changes
```

若格式不符，可先執行：

```powershell
dotnet format MyDrawVector.slnx
```

提交前請確認格式驗證通過。

---

## 6. 執行與除錯

### 6.1 直接執行

```powershell
dotnet run --project src\MyDrawVector\MyDrawVector.csproj -c Debug
```

或執行建置輸出：

```text
src\MyDrawVector\bin\Debug\net10.0-windows10.0.19041.0\win-x64\MyDrawVector.exe
```

### 6.2 日誌

- 位置：`%LocalAppData%\MyDrawVector\Logs\app.log`
- 超過 1 MB 會輪替為 `app.log.1`
- 未處理例外會記錄完整堆疊

### 6.3 設定

- 位置：`%AppData%\MyDrawVector\settings.json`
- 若要重置設定，關閉程式後刪除該檔案。

### 6.4 自動儲存

- 位置：`%LocalAppData%\MyDrawVector\Autosave`
- `autosave.mydraw`：文件內容
- `autosave.json`：原始路徑與時間

---

## 7. 發行可攜式版本

本專案發行 **非 self-contained** 可攜式資料夾，提供 x64 與 ARM64 兩種架構。

### 7.1 x64

```powershell
dotnet publish src\MyDrawVector\MyDrawVector.csproj -c Release -r win-x64 --self-contained false `
  -p:WindowsAppSDKSelfContained=false -p:WindowsPackageType=None `
  -p:PublishSingleFile=false -p:PublishTrimmed=false `
  -o publish\MyDrawVector-portable-win-x64
```

### 7.2 ARM64

```powershell
dotnet publish src\MyDrawVector\MyDrawVector.csproj -c Release -r win-arm64 --self-contained false `
  -p:WindowsAppSDKSelfContained=false -p:WindowsPackageType=None `
  -p:PublishSingleFile=false -p:PublishTrimmed=false `
  -o publish\MyDrawVector-portable-win-arm64
```

### 7.3 發行注意事項

- `MyDrawVector.pri` 必須存在於輸出資料夾，否則 Release 版啟動時會發生 XAML 解析錯誤。`CopyAppPriToPublish` target 會自動複製。
- 請勿只複製 `MyDrawVector.exe`；`Assets` 與所有 DLL 都是執行所需。
- x64 與 ARM64 的輸出不可混用。
- 發行後可檢查 `MyDrawVector.exe` 的 PE 架構與檔案版本。

### 7.4 建立 zip

```powershell
Compress-Archive -Path publish\MyDrawVector-portable-win-x64\* `
  -DestinationPath MyDrawVector-1.6.12-portable-win-x64.zip -Force

Compress-Archive -Path publish\MyDrawVector-portable-win-arm64\* `
  -DestinationPath MyDrawVector-1.6.12-portable-win-arm64.zip -Force
```

建議在 zip 內附上可攜版使用說明，並確認解壓縮後 `MyDrawVector.exe` 位於根目錄。

### 7.5 發行前檢查

- [ ] `dotnet build MyDrawVector.slnx -c Debug` 成功
- [ ] `dotnet test MyDrawVector.slnx -c Debug` 全數通過
- [ ] `dotnet format MyDrawVector.slnx --verify-no-changes` 通過
- [ ] x64 與 ARM64 均能發行
- [ ] 兩個輸出資料夾都有 `MyDrawVector.pri`、`Assets\app.ico` 與 DLL
- [ ] zip 可解壓縮並執行
- [ ] 版本號與 `Directory.Build.props`、`app.manifest` 一致
- [ ] `CHANGELOG.md`、`RELEASE_NOTES.md` 已更新

---

## 8. 版本規則

版本集中在 `Directory.Build.props`：

```xml
<Version>1.6.12</Version>
<AssemblyVersion>1.6.0.0</AssemblyVersion>
<FileVersion>1.6.12.0</FileVersion>
```

- `Version`：`主.副.修`，每次功能變更遞增。
- `FileVersion`：跟隨 `Version`。
- `AssemblyVersion`：僅主／副版變更時調整。
- `app.manifest` 的 `assemblyIdentity` 版本需與 `FileVersion` 同步。
- 同步更新 `CHANGELOG.md`、`RELEASE_NOTES.md` 與 `docs/DEV_LOG.md`。

---

## 9. 程式碼規範

- 4 空格縮排。
- 類別與方法使用 PascalCase；區域變數與參數使用 camelCase；私有欄位使用 `_camelCase`。
- 非同步方法以 `Async` 結尾。
- 不在 UI 執行緒做長時間 I/O。
- 例外要記錄並處理，不要吞例外。
- 使用者可見字串放入 `Strings/zh-Hant/Resources.resw`，以 `x:Uid` 或 `ResourceLoader` 取得。
- 顏色與筆刷使用資源或共用色盤，不要在 XAML 隨意寫死色碼。
- 可點擊元素要有 `AutomationProperties.Name`，圖示按鈕要有工具提示。
- 不要引入未在 `Directory.Packages.props` 宣告的套件。

---

## 10. 字串與資源

- 預設語言：`zh-Hant`。
- 資源檔：`src/MyDrawVector/Strings/zh-Hant/Resources.resw`。
- XAML 以 `x:Uid` 綁定；程式碼使用 `ResourceLoader`。
- 新增字串時請同時確認：
  - XAML 的 `x:Uid` 名稱正確。
  - 工具提示與無障礙名稱都有對應資源。
  - 不要留下未使用的舊字串。

---

## 11. 檔案與資料位置

| 資料 | 位置 |
| --- | --- |
| 使用者設定 | `%AppData%\MyDrawVector\settings.json` |
| 日誌 | `%LocalAppData%\MyDrawVector\Logs\app.log` |
| 自動儲存 | `%LocalAppData%\MyDrawVector\Autosave` |
| 檔案關聯 | `HKCU\Software\Classes\.mydraw` 與相關 ProgID |

請勿把上述執行期資料提交到版本控制。

---

## 12. 常見問題

### 12.1 啟動時出現 XAML 解析錯誤

- 確認輸出資料夾有 `MyDrawVector.pri`。
- 確認 `Assets\app.ico` 存在。
- 重新執行 `dotnet publish`，不要手動複製零散檔案。

### 12.2 執行時找不到 Runtime

- 安裝 .NET 10 Desktop Runtime。
- 安裝 Windows App SDK 2.6.5 Runtime。
- 確認架構與程式版本一致。

### 12.3 ARM64 發行失敗

- 確認 SDK 支援 `win-arm64`。
- 確認 `RuntimeIdentifiers` 包含 `win-arm64`。
- 使用 `-r win-arm64` 發行，不要只改 `Platform`。

### 12.4 檔案關聯沒有更新

- 重新執行 exe。
- 或使用 `MyDrawVector.exe --register`。
- 或由偏好設定手動建立／更新。

### 12.5 設定沒有保存

- 確認 `%AppData%\MyDrawVector` 可寫入。
- 查看日誌是否有寫入失敗訊息。

---

## 13. 貢獻流程

1. 先開 Issue 描述問題或需求。
2. 建立分支進行修改。
3. 保持變更範圍集中，不要更動無關檔案。
4. 更新測試與文件。
5. 執行建置、測試與格式驗證。
6. 提交 Pull Request，說明動機、變更內容與驗證方式。

請勿提交 `bin/`、`obj/`、`publish/`、`artifacts/`、`.vs/`、使用者設定、日誌、憑證或任何密鑰。

---

## 14. 授權

本專案採用 MIT License，詳細內容請參閱根目錄的 [LICENSE](../LICENSE)。提交貢獻即表示同意以相同授權釋出。
