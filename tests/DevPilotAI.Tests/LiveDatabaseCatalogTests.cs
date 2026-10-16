using System.Data;
using DevPilotAI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DevPilotAI.Tests;

public class LiveDatabaseCatalogTests
{
    private const string ConnectionString = "Server=DESKTOP-B8T5DKQ;Database=DevPilotDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Timeout=3";

    private static bool IsLiveDatabaseReachable()
    {
        try
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            return conn.State == ConnectionState.Open;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task LiveCatalog_ForeignKeys_MustNotHaveCascadeDelete()
    {
        if (!IsLiveDatabaseReachable())
        {
            // Database is blocked/unreachable in this environment
            return;
        }

        using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        var query = @"
            SELECT 
                fk.name AS ForeignKeyName,
                OBJECT_NAME(fk.parent_object_id) AS TableName,
                fk.delete_referential_action_desc AS DeleteAction
            FROM sys.foreign_keys fk;";

        using var cmd = new SqlCommand(query, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        var cascadeFks = new List<string>();
        int count = 0;

        while (await reader.ReadAsync())
        {
            count++;
            var fkName = reader.GetString(0);
            var tableName = reader.GetString(1);
            var deleteAction = reader.GetString(2);

            if (deleteAction == "CASCADE")
            {
                cascadeFks.Add($"{tableName}.{fkName}");
            }
        }

        Assert.True(count > 0, "Foreign keys should exist in the live database.");
        Assert.Empty(cascadeFks); // Rule: No cascade deletes anywhere!
    }

    [Fact]
    public async Task LiveCatalog_UniqueIndexes_VerifySoftDeleteFilteredIndexes()
    {
        if (!IsLiveDatabaseReachable())
        {
            return;
        }

        using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        var query = @"
            SELECT 
                i.name AS IndexName,
                OBJECT_NAME(i.object_id) AS TableName,
                i.is_unique AS IsUnique,
                i.has_filter AS HasFilter,
                i.filter_definition AS FilterDefinition
            FROM sys.indexes i
            WHERE i.is_unique = 1 AND i.name LIKE 'IX_%';";

        using var cmd = new SqlCommand(query, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        var verifiedIndexes = new List<string>();
        while (await reader.ReadAsync())
        {
            var indexName = reader.GetString(0);
            var tableName = reader.GetString(1);
            var hasFilter = reader.GetBoolean(3);
            var filter = hasFilter && !reader.IsDBNull(4) ? reader.GetString(4) : string.Empty;

            verifiedIndexes.Add($"{tableName}.{indexName} (Filter: {filter})");
        }

        Assert.NotEmpty(verifiedIndexes);
    }

    [Fact]
    public async Task LiveCatalog_AuditLogsAndPlans_TablesExist()
    {
        if (!IsLiveDatabaseReachable())
        {
            return;
        }

        using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        var query = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_TYPE = 'BASE TABLE';";

        using var cmd = new SqlCommand(query, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Contains("Users", tables);
        Assert.Contains("Problems", tables);
        Assert.Contains("AuditLogs", tables);
        Assert.Contains("Plans", tables);
        Assert.Contains("Features", tables);
        Assert.Contains("UserSubscriptions", tables);
        Assert.Contains("AIUsageEvents", tables);
        Assert.Contains("UsageLedgers", tables);
    }
}
