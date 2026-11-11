MyDrawVector（我的向量畫布）1.6.12 可攜式版
==========================================

本資料夾為免安裝版本，不需要系統管理員權限。請確認下載的是正確架構：
- x64：適用一般 Intel／AMD Windows 電腦
- ARM64：適用 Windows on ARM 裝置

系統需求
--------
- Windows 10 1809（build 17763）以上
- .NET 10 Desktop Runtime（x64 或 ARM64）
- Windows App SDK 2.6.5 Runtime（x64 或 ARM64）

本版本為非 self-contained 發行，執行前必須安裝上述兩個執行環境。

使用方式
--------
1. 將整個資料夾解壓縮到任意位置。
2. 執行 MyDrawVector.exe。
3. 第一次啟動會自動建立 .mydraw 檔案關聯（只寫入目前使用者登錄區）。
4. 之後可直接雙擊 .mydraw 檔案開啟。
5. 若搬移資料夾後檔案關聯失效，重新執行一次 MyDrawVector.exe，或由
   「設定 → 偏好設定 → 建立／更新檔案關聯」修正。

重要
----
- 請保留整個資料夾，不要只複製 MyDrawVector.exe。
- 必須保留 MyDrawVector.pri、Assets 資料夾與所有 DLL。
- 若程式無法啟動，請先確認已安裝對應架構的 .NET 10 Desktop Runtime
  與 Windows App SDK 2.6.5 Runtime。
- 日誌位置：%LocalAppData%\MyDrawVector\Logs\app.log
- 設定位置：%AppData%\MyDrawVector\settings.json
- 自動儲存：%LocalAppData%\MyDrawVector\Autosave

隱私
----
MyDrawVector 完全離線運作，不會上傳作品、圖片、檔案路徑或使用紀錄。

授權
----
MIT License，Copyright (c) 2026 mark216tw。
詳細內容請參閱專案根目錄的 LICENSE。

更多說明
--------
https://github.com/mark216tw/win-mydrawvector-01
