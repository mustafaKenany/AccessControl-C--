using Npgsql;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AccessControlPro.Web.Data;

public static class SyncHelper
{
    // Per-request error list. Must be AsyncLocal (not plain static) because concurrent
    // /api/sync requests from different gyms would otherwise share the same list and
    // leak errors across tenants. Each request calls ResetErrors() at entry to start
    // with a fresh list, then accumulates errors during that request's processing.
    private static readonly AsyncLocal<List<string>?> _asyncErrors = new();
    private static List<string> Errors => _asyncErrors.Value ??= new List<string>();

    // Whitelist of allowed table names to prevent SQL injection
    private static readonly HashSet<string> _allowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Players", "AccessEvents", "Devices", "Doors", "Transactions",
        "Users", "AuditLogs", "DeletedEmployees", "AppSettings", "AccessCards",
        "CloudSyncLogs", "QrPasses", "QrPool", "Gyms", "SubscriptionPlans", "PosShifts",
        "FreezeHistories", "Products", "TimeGroups"
    };

    // Column name must be alphanumeric/underscore only
    private static readonly Regex _validColumnName = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static List<string> GetLastErrors() => _asyncErrors.Value ?? new List<string>();

    /// <summary>Clear the per-request error list. Call at the start of each /api/sync request.</summary>
    public static void ResetErrors() => _asyncErrors.Value = new List<string>();

    public static async Task<int> UpsertRowsAsync(NpgsqlConnection conn, string tableName,
        List<Dictionary<string, object?>> rows, bool isFullSync = true)
    {
        // Validate table name against whitelist
        if (!_allowedTables.Contains(tableName))
        {
            Errors.Add($"Rejected invalid table name: {tableName}");
            return 0;
        }

        // Use transaction to ensure atomicity — if inserts fail, delete is rolled back
        using var txn = await conn.BeginTransactionAsync();
        try
        {
            // Full sync: wipe the table so absent-from-payload rows are treated as deleted.
            // Delta sync: keep existing rows; upsert just the changed ones.
            if (isFullSync)
            {
                try
                {
                    using var del = new NpgsqlCommand($@"DELETE FROM ""{tableName}""", conn);
                    del.Transaction = txn as NpgsqlTransaction;
                    await del.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Errors.Add($"{tableName} DELETE: {ex.Message}");
                    await txn.RollbackAsync();
                    return 0;
                }
            }

            // Delta sync with 0 changed rows is a no-op — nothing to commit.
            // Full sync with 0 rows still commits the delete above (wipes the cloud copy).
            if (rows.Count == 0)
            {
                await txn.CommitAsync();
                return 0;
            }

            int count = 0;
            int errors = 0;
            int rowIndex = 0;
            foreach (var row in rows)
            {
                rowIndex++;
                var savepointName = $"sp_{rowIndex}";

                // Create a savepoint so one bad row doesn't poison the whole transaction (PG 25P02 cascade)
                try
                {
                    using var save = new NpgsqlCommand($"SAVEPOINT {savepointName}", conn);
                    save.Transaction = txn as NpgsqlTransaction;
                    await save.ExecuteNonQueryAsync();
                }
                catch { /* if savepoint creation fails, the outer try/catch handles it */ }

                try
                {
                    var cols = new List<string>();
                    var vals = new List<string>();
                    var updateAssigns = new List<string>();
                    var pars = new List<NpgsqlParameter>();
                    int i = 0;

                    foreach (var kvp in row)
                    {
                        // Validate column name to prevent SQL injection
                        if (!_validColumnName.IsMatch(kvp.Key) || kvp.Key.Length > 100)
                        {
                            Errors.Add($"{tableName}: Rejected invalid column name '{kvp.Key}'");
                            continue;
                        }

                        cols.Add($@"""{kvp.Key}""");
                        vals.Add($"@p{i}");
                        if (!kvp.Key.Equals("Id", StringComparison.OrdinalIgnoreCase))
                            updateAssigns.Add($@"""{kvp.Key}"" = EXCLUDED.""{kvp.Key}""");

                        object? val = kvp.Value;

                        // Handle JsonElement values from deserialization
                        if (val is JsonElement je)
                        {
                            val = ConvertJsonElement(je, kvp.Key);
                        }

                        pars.Add(new NpgsqlParameter($"p{i}", val ?? DBNull.Value));
                        i++;
                    }

                    if (cols.Count == 0)
                    {
                        using var release = new NpgsqlCommand($"RELEASE SAVEPOINT {savepointName}", conn);
                        release.Transaction = txn as NpgsqlTransaction;
                        await release.ExecuteNonQueryAsync();
                        continue;
                    }

                    // ON CONFLICT handling: if there are non-Id columns to update, use DO UPDATE.
                    // If every incoming column was rejected except Id (edge case), use DO NOTHING —
                    // `DO UPDATE SET ` with an empty SET clause is a SQL syntax error in PostgreSQL.
                    var conflictClause = updateAssigns.Count > 0
                        ? $@" ON CONFLICT (""Id"") DO UPDATE SET {string.Join(",", updateAssigns)}"
                        : @" ON CONFLICT (""Id"") DO NOTHING";
                    var sql = $@"INSERT INTO ""{tableName}"" ({string.Join(",", cols)}) VALUES ({string.Join(",", vals)}){conflictClause}";
                    using var cmd = new NpgsqlCommand(sql, conn);
                    cmd.Transaction = txn as NpgsqlTransaction;
                    cmd.Parameters.AddRange(pars.ToArray());
                    await cmd.ExecuteNonQueryAsync();

                    using var rel = new NpgsqlCommand($"RELEASE SAVEPOINT {savepointName}", conn);
                    rel.Transaction = txn as NpgsqlTransaction;
                    await rel.ExecuteNonQueryAsync();
                    count++;
                }
                catch (Exception ex)
                {
                    errors++;
                    // Roll back to savepoint so the transaction stays usable for subsequent rows
                    try
                    {
                        using var rb = new NpgsqlCommand($"ROLLBACK TO SAVEPOINT {savepointName}", conn);
                        rb.Transaction = txn as NpgsqlTransaction;
                        await rb.ExecuteNonQueryAsync();
                    }
                    catch { /* if rollback fails, the whole transaction is dead — outer catch handles it */ }

                    if (errors <= 3)
                        Errors.Add($"{tableName} INSERT row {rowIndex}: {ex.Message}");
                }
            }

            // Commit even with partial errors — better to have some data than none
            await txn.CommitAsync();

            if (errors > 0)
                Errors.Add($"{tableName}: {errors}/{rows.Count} rows failed");

            return count;
        }
        catch (Exception ex)
        {
            Errors.Add($"{tableName} TRANSACTION: {ex.Message}");
            try { await txn.RollbackAsync(); } catch { }
            return 0;
        }
    }

    /// <summary>
    /// Applies soft-delete tombstones to a target table. For each row in <paramref name="tombstones"/>,
    /// reads the value at <paramref name="idFieldInTombstone"/> (e.g. "OriginalId") and deletes
    /// the matching row from <paramref name="targetTable"/>. Used in delta-sync mode where the
    /// target table isn't wiped upfront — deletes must flow through the tombstone table instead.
    /// </summary>
    public static async Task ApplyDeleteTombstonesAsync(NpgsqlConnection conn, string targetTable,
        string idFieldInTombstone, List<Dictionary<string, object?>> tombstones)
    {
        if (!_allowedTables.Contains(targetTable))
        {
            Errors.Add($"Tombstone: rejected target table {targetTable}");
            return;
        }
        if (!_validColumnName.IsMatch(idFieldInTombstone))
        {
            Errors.Add($"Tombstone: rejected field {idFieldInTombstone}");
            return;
        }

        foreach (var ts in tombstones)
        {
            if (!ts.TryGetValue(idFieldInTombstone, out var idVal) || idVal == null) continue;

            object? extracted = idVal is JsonElement je ? ConvertJsonElement(je, idFieldInTombstone) : idVal;
            if (extracted == null || extracted is DBNull) continue;

            try
            {
                using var cmd = new NpgsqlCommand($@"DELETE FROM ""{targetTable}"" WHERE ""Id"" = @id", conn);
                cmd.Parameters.AddWithValue("id", extracted);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Errors.Add($"{targetTable} tombstone delete (id={extracted}): {ex.Message}");
            }
        }
    }

    private static object? ConvertJsonElement(JsonElement je, string columnName)
    {
        switch (je.ValueKind)
        {
            case JsonValueKind.String:
                var str = je.GetString();
                if (str == null) return DBNull.Value;

                // Try to parse as DateTime for date/time columns
                if (IsDateColumn(columnName) && DateTime.TryParse(str, out var dt))
                    return dt;

                // Try to parse as TimeSpan for time columns
                if (IsTimeColumn(columnName) && TimeSpan.TryParse(str, out var ts))
                    return ts;

                return str;

            case JsonValueKind.Number:
                // For decimal columns, use decimal
                if (IsDecimalColumn(columnName))
                    return je.GetDecimal();
                // For integer columns
                if (je.TryGetInt32(out var intVal))
                    return intVal;
                if (je.TryGetInt64(out var longVal))
                    return longVal;
                return je.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return DBNull.Value;

            default:
                return je.ToString();
        }
    }

    private static bool IsDateColumn(string name)
    {
        return name.Contains("Date", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Time", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("At", StringComparison.OrdinalIgnoreCase) ||
               name == "Timestamp" || name == "EventDate" ||
               name == "CreatedAt" || name == "DeletedAt" ||
               name == "FreezeStart" || name == "FreezeEnd" ||
               name == "FreezeStartDate" || name == "FreezeEndDate" || name == "ValidFrom" || name == "ValidTo" ||
               name == "TransactionDate" || name == "SyncedAt" || name == "ExpiresAt" ||
               name == "AssignedAt" || name == "ExpiredAt" || name == "UsedAt" ||
               name == "OpenedAt" || name == "ClosedAt";
    }

    private static bool IsTimeColumn(string name)
    {
        return name == "WorkStartTime" || name == "WorkEndTime";
    }

    private static bool IsDecimalColumn(string name)
    {
        return name.Contains("Fee", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Paid", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Height", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Weight", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Balance", StringComparison.OrdinalIgnoreCase) ||
               name == "SubscriptionFee" || name == "AmountPaid" ||
               name.Contains("Cash", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Sales", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Variance", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Opening", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Closing", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Discount", StringComparison.OrdinalIgnoreCase) ||
               name == "Price" || name == "Cost" || name == "Stock";
    }
}
