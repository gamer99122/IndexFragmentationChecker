# SQL Server 索引碎裂檢查工具

## 功能說明

這是一個簡單的 C# .NET 8 主控台程式，用於檢查 SQL Server 資料庫的索引碎裂情況並提供維護建議。

## 功能特色

- 使用 Windows 認證連線 SQL Server
- 列出 TOP 30 碎裂率最高的索引
- 自動分析並提供維護建議：
  - HEAP 表建議建立叢集索引
  - 碎裂率 > 30% 建議 REBUILD
  - 碎裂率 5-30% 建議 REORGANIZE
  - 碎裂率 < 5% 無需處理
- 彩色輸出，方便識別問題嚴重程度
- 提供詳細的 SQL 維護指令

## 系統需求

- .NET 8 SDK
- SQL Server (任何版本)
- Windows 認證權限

## 建置與執行

### 建置專案
```bash
dotnet build
```

### 執行程式
```bash
dotnet run
```

### 發佈為單一執行檔
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 使用方式

1. 執行程式
2. 輸入 SQL Server 名稱（例如：`localhost` 或 `.\\SQLEXPRESS`）
3. 輸入資料庫名稱
4. 查看分析結果和建議

## 輸出說明

### 顏色標示
- **紅色**: 碎裂率 > 30%，需要立即處理
- **黃色**: 碎裂率 5-30%，建議處理
- **白色**: 碎裂率 < 5%，狀態良好

### 建議說明

| 建議 | 說明 | 適用情況 |
|------|------|---------|
| 建立叢集索引 | HEAP 表需要建立叢集索引 | HEAP 且碎裂率 > 30% |
| REBUILD | 重建索引 | 碎裂率 > 30% 且頁數 > 1000 |
| REORGANIZE | 重組索引 | 碎裂率 5-30% 且頁數 > 1000 |
| 無需處理 | 索引狀態良好或資料量太小 | 碎裂率 < 5% 或頁數 < 1000 |

## 注意事項

1. 頁數 < 1000 的索引即使碎裂率高也不建議維護（影響不大）
2. REBUILD 操作會鎖定資料表，建議在離峰時段執行
3. REORGANIZE 是線上操作，對系統影響較小
4. HEAP 表建議建立叢集索引以根本解決碎裂問題

## 範例輸出

```
========================================================================================================================
資料表名稱                      索引名稱                        索引類型           碎裂率%     頁數 建議
========================================================================================================================
Orders                        NULL                          HEAP                 96.67     5216 建立叢集索引
Products_bak                  NULL                          HEAP                 66.67        9 無需處理
Customers                     PK_Customers                  CLUSTERED            45.23     3450 REBUILD
SalesDetails                  IX_SalesDetails_Date          NONCLUSTERED         25.18     1520 REORGANIZE
Employees                     PK_Employees                  CLUSTERED            19.05       21 無需處理
Inventory                     IX_Inventory_ProductID        NONCLUSTERED          3.42      850 狀態良好
========================================================================================================================

統計摘要:
  需要 REBUILD 的索引: 1 個
  需要 REORGANIZE 的索引: 1 個
  需要處理的 HEAP 表: 1 個

詳細維護建議:
------------------------------------------------------------------------------------------------------------------------

【高優先級】HEAP 表需要建立叢集索引:
  - Orders (碎裂率: 96.67%, 頁數: 5216)
    建議: CREATE CLUSTERED INDEX CIX_Orders ON Orders(主鍵欄位);

【需要 REBUILD】碎裂率 > 30% 且頁數 > 1000:
  - Customers.PK_Customers (碎裂率: 45.23%, 頁數: 3450)
    執行: ALTER INDEX PK_Customers ON Customers REBUILD;

【需要 REORGANIZE】碎裂率 5-30% 且頁數 > 1000:
  - SalesDetails.IX_SalesDetails_Date (碎裂率: 25.18%, 頁數: 1520)
    執行: ALTER INDEX IX_SalesDetails_Date ON SalesDetails REORGANIZE;
------------------------------------------------------------------------------------------------------------------------
```

## 授權

此程式僅供參考使用。

## 備註

此小工具由 Claude AI 協助產生。