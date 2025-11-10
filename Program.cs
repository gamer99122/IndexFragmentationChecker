using Microsoft.Data.SqlClient;
using System.Data;

// 設定連線字串（使用 Windows 認證）
Console.Write("請輸入 SQL Server 名稱 (例如: localhost 或 .\\SQLEXPRESS): ");
string? serverName = Console.ReadLine();

Console.Write("請輸入資料庫名稱: ");
string? databaseName = Console.ReadLine();

if (string.IsNullOrWhiteSpace(serverName) || string.IsNullOrWhiteSpace(databaseName))
{
    Console.WriteLine("伺服器名稱或資料庫名稱不可為空！");
    return;
}

string connectionString = $"Server={serverName};Database={databaseName};Integrated Security=true;TrustServerCertificate=true;";

Console.WriteLine("\n正在連線資料庫...\n");

try
{
    using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    string query = @"
        SELECT TOP 30
            t.name AS TableName, 
            i.name AS IndexName,
            i.type_desc AS IndexType,
            ps.avg_fragmentation_in_percent AS FragmentationPercent,
            ps.page_count AS PageCount
        FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') AS ps
        JOIN sys.indexes AS i 
            ON ps.object_id = i.object_id AND ps.index_id = i.index_id
        JOIN sys.tables AS t 
            ON i.object_id = t.object_id
        WHERE ps.database_id = DB_ID()
        ORDER BY ps.avg_fragmentation_in_percent DESC;
    ";

    using var command = new SqlCommand(query, connection);
    using var reader = await command.ExecuteReaderAsync();

    var results = new List<IndexFragmentation>();

    while (await reader.ReadAsync())
    {
        results.Add(new IndexFragmentation
        {
            TableName = reader["TableName"].ToString() ?? "",
            IndexName = reader["IndexName"].ToString() ?? "NULL",
            IndexType = reader["IndexType"].ToString() ?? "",
            FragmentationPercent = reader["FragmentationPercent"] != DBNull.Value
                ? Convert.ToDouble(reader["FragmentationPercent"])
                : 0,
            PageCount = reader["PageCount"] != DBNull.Value
                ? Convert.ToInt32(reader["PageCount"])
                : 0
        });
    }

    // 顯示結果
    Console.WriteLine("=" + new string('=', 120));
    Console.WriteLine($"{"資料表名稱",-30} {"索引名稱",-30} {"索引類型",-15} {"碎裂率%",10} {"頁數",8} {"建議",20}");
    Console.WriteLine("=" + new string('=', 120));

    foreach (var item in results)
    {
        string recommendation = GetRecommendation(item);
        var color = GetConsoleColor(item.FragmentationPercent);

        Console.ForegroundColor = color;
        Console.WriteLine($"{item.TableName,-30} {item.IndexName,-30} {item.IndexType,-15} {item.FragmentationPercent,10:F2} {item.PageCount,8} {recommendation,20}");
        Console.ResetColor();
    }

    Console.WriteLine("=" + new string('=', 120));

    // 統計摘要
    var needRebuild = results.Where(r => r.FragmentationPercent > 30 && r.PageCount > 1000).ToList();
    var needReorganize = results.Where(r => r.FragmentationPercent >= 5 && r.FragmentationPercent <= 30 && r.PageCount > 1000).ToList();
    var heapTables = results.Where(r => r.IndexType == "HEAP" && r.FragmentationPercent > 30).ToList();

    Console.WriteLine("\n統計摘要:");
    Console.WriteLine($"  需要 REBUILD 的索引: {needRebuild.Count} 個");
    Console.WriteLine($"  需要 REORGANIZE 的索引: {needReorganize.Count} 個");
    Console.WriteLine($"  需要處理的 HEAP 表: {heapTables.Count} 個");

    // 顯示詳細建議
    if (needRebuild.Any() || needReorganize.Any() || heapTables.Any())
    {
        Console.WriteLine("\n詳細維護建議:");
        Console.WriteLine(new string('-', 120));

        if (heapTables.Any())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n【高優先級】HEAP 表需要建立叢集索引:");
            Console.ResetColor();
            foreach (var item in heapTables)
            {
                Console.WriteLine($"  - {item.TableName} (碎裂率: {item.FragmentationPercent:F2}%, 頁數: {item.PageCount})");
                Console.WriteLine($"    建議: CREATE CLUSTERED INDEX CIX_{item.TableName} ON {item.TableName}(主鍵欄位);");
            }
        }

        if (needRebuild.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n【需要 REBUILD】碎裂率 > 30% 且頁數 > 1000:");
            Console.ResetColor();
            foreach (var item in needRebuild)
            {
                Console.WriteLine($"  - {item.TableName}.{item.IndexName} (碎裂率: {item.FragmentationPercent:F2}%, 頁數: {item.PageCount})");
                Console.WriteLine($"    執行: ALTER INDEX {item.IndexName} ON {item.TableName} REBUILD;");
            }
        }

        if (needReorganize.Any())
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n【需要 REORGANIZE】碎裂率 5-30% 且頁數 > 1000:");
            Console.ResetColor();
            foreach (var item in needReorganize)
            {
                Console.WriteLine($"  - {item.TableName}.{item.IndexName} (碎裂率: {item.FragmentationPercent:F2}%, 頁數: {item.PageCount})");
                Console.WriteLine($"    執行: ALTER INDEX {item.IndexName} ON {item.TableName} REORGANIZE;");
            }
        }

        Console.WriteLine(new string('-', 120));
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n✓ 所有索引狀態良好，無需維護！");
        Console.ResetColor();
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n錯誤: {ex.Message}");
    Console.ResetColor();
}

Console.WriteLine("\n按任意鍵結束...");
Console.ReadKey();

// 輔助方法
static string GetRecommendation(IndexFragmentation item)
{
    if (item.IndexType == "HEAP" && item.FragmentationPercent > 30)
        return "建立叢集索引";

    if (item.PageCount < 1000)
        return "無需處理";

    if (item.FragmentationPercent > 30)
        return "REBUILD";

    if (item.FragmentationPercent >= 5)
        return "REORGANIZE";

    return "狀態良好";
}

static ConsoleColor GetConsoleColor(double fragmentationPercent)
{
    return fragmentationPercent switch
    {
        > 30 => ConsoleColor.Red,
        >= 5 => ConsoleColor.Yellow,
        _ => ConsoleColor.White
    };
}

// 資料模型
record IndexFragmentation
{
    public string TableName { get; init; } = "";
    public string IndexName { get; init; } = "";
    public string IndexType { get; init; } = "";
    public double FragmentationPercent { get; init; }
    public int PageCount { get; init; }
}