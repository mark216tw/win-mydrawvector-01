# 貢獻指南

感謝你對 MyDrawVector 的興趣。本專案以 MIT License 釋出，歡迎回報問題、提出建議與提交 Pull Request。

## 回報問題

請在 GitHub Issues 提供：

- 問題描述與重現步驟。
- 預期結果與實際結果。
- Windows 版本、架構（x64／ARM64）、程式版本。
- 相關日誌：`%LocalAppData%\MyDrawVector\Logs\app.log`。
- 若涉及畫面，請附上截圖（請先確認不含個人資料）。

## 開發流程

1. 先開 Issue 討論需求或問題。
2. 建立分支，保持變更範圍集中。
3. 更新程式、測試與文件。
4. 執行建置、測試與格式驗證。
5. 提交 Pull Request，說明動機、變更內容與驗證方式。

```powershell
dotnet build MyDrawVector.slnx -c Debug
dotnet test MyDrawVector.slnx -c Debug
dotnet format MyDrawVector.slnx --verify-no-changes
```

## 程式碼規範

- 使用 C# 與 .NET 10，啟用 Nullable 與 TreatWarningsAsErrors。
- 4 空格縮排；類別／方法 PascalCase、區域變數 camelCase、私有欄位 `_camelCase`。
- 非同步方法以 `Async` 結尾。
- 使用者可見字串放入 `src/MyDrawVector/Strings/zh-Hant/Resources.resw`。
- 不要引入未在 `Directory.Packages.props` 宣告的套件。
- 不要留下 `TODO`、測試假資料、`Console.WriteLine` 或 `Debug.WriteLine`。
- 變更功能時同步更新 `docs/DEV_LOG.md`、`CHANGELOG.md` 與 `RELEASE_NOTES.md`。

## 不要提交的內容

- `bin/`、`obj/`、`publish/`、`artifacts/`、`.vs/`
- 使用者設定、日誌、自動儲存檔
- 憑證、金鑰、密碼、`.pfx`、`.snk`
- 與本次變更無關的檔案

## 授權

提交貢獻即表示同意以 MIT License 釋出你的貢獻。
