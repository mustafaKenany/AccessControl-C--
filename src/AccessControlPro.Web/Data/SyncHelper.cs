using Npgsql;
using System.Text.Json;

namespace AccessControlPro.Web.Data;

public static class SyncHelper
{
    public static async Task<int> UpsertRowsAsync(NpgsqlConnection conn, string tableName,
        List<Dictionary<string, object?>> rows)
    {
        if (rows.Count == 0) return 0;

        // Delete existing data first (full sync)
        try
        {
            using var del = new NpgsqlCommand($@"DELETE FROM ""{tableName}""", conn);
            await del.ExecuteNonQueryAsync();
        }
        catch { }

        int count = 0;
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
                    cols.Add($@"""{kvp.Key}""");
                    vals.Add($"@p{i}");

                    object? val = kvp.Value;

                    // Handle JsonElement values from deserialization
                    if (val is JsonElement je)
                    {
                        val = je.ValueKind switch
                        {
                            JsonValueKind.String => je.GetString(),
                            JsonValueKind.Number => je.TryGetInt64(out var l) ? (object)l : je.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => DBNull.Value,
                            _ => je.ToString()
                        };
                    }

                    // Handle decimal conversion for numeric fields
                    if (val is double d && (kvp.Key.Contains("Fee") || kvp.Key.Contains("Amount") ||
                        kvp.Key.Contains("Paid") || kvp.Key.Contains("Height") || kvp.Key.Contains("Weight") ||
                        kvp.Key.Contains("Balance")))
                    {
                        val = (decimal)d;
                    }

                    pars.Add(new NpgsqlParameter($"p{i}", val ?? DBNull.Value));
                    i++;
                }

                var sql = $@"INSERT INTO ""{tableName}"" ({string.Join(",", cols)}) VALUES ({string.Join(",", vals)})";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddRange(pars.ToArray());
                await cmd.ExecuteNonQueryAsync();
                count++;
            }
            catch { }
        }

        return count;
    }
}
