using Npgsql;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AccessControlPro.Web.Data;

public static class SyncHelper
{
    private static readonly List<string> _lastErrors = new();

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

    public static List<string> GetLastErrors() => _lastErrors;

    public static async Task<int> UpsertRowsAsync(NpgsqlConnection conn, string tableName,
        List<Dictionary<string, object?>> rows)
    {
        if (rows.Count == 0) return 0;

        // Validate table name against whitelist
        if (!_allowedTables.Contains(tableName))
        {
            _lastErrors.Add($"Rejected invalid table name: {tableName}");
            return 0;
        }

        // Delete existing data first (full sync)
        try
        {
            using var del = new NpgsqlCommand($@"DELETE FROM ""{tableName}""", conn);
            await del.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _lastErrors.Add($"{tableName} DELETE: {ex.Message}");
        }

        int count = 0;
        int errors = 0;
        foreach (var row in rows)
        {
            try
            {
                var cols = new List<string>();
                var vals = new List<string>();
                var pars = new List<NpgsqlParameter>();
                int i = 0;

                foreach (var kvp in row)
                {
                    // Validate column name to prevent SQL injection
                    if (!_validColumnName.IsMatch(kvp.Key) || kvp.Key.Length > 100)
                    {
                        _lastErrors.Add($"{tableName}: Rejected invalid column name '{kvp.Key}'");
                        continue;
                    }

                    cols.Add($@"""{kvp.Key}""");
                    vals.Add($"@p{i}");

                    object? val = kvp.Value;

                    // Handle JsonElement values from deserialization
                    if (val is JsonElement je)
                    {
                        val = ConvertJsonElement(je, kvp.Key);
                    }

                    pars.Add(new NpgsqlParameter($"p{i}", val ?? DBNull.Value));
                    i++;
                }

                if (cols.Count == 0) continue;

                var sql = $@"INSERT INTO ""{tableName}"" ({string.Join(",", cols)}) VALUES ({string.Join(",", vals)})";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddRange(pars.ToArray());
                await cmd.ExecuteNonQueryAsync();
                count++;
            }
            catch (Exception ex)
            {
                errors++;
                if (errors <= 3)
                    _lastErrors.Add($"{tableName} INSERT row {count + errors}: {ex.Message}");
            }
        }

        if (errors > 0)
            _lastErrors.Add($"{tableName}: {errors}/{rows.Count} rows failed");

        return count;
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
