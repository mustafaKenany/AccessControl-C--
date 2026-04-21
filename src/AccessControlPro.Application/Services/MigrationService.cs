using System.Data;
using System.Text.RegularExpressions;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.Data.SqlClient;

namespace AccessControlPro.Application.Services;

public class MigrationService : IMigrationService
{
    private readonly IEmployeeRepository _employeeRepository;

    public MigrationService(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    /// <summary>Validates table name to prevent SQL injection (alphanumeric + underscore only).</summary>
    private static void ValidateTableName(string tableName)
    {
        if (!Regex.IsMatch(tableName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            throw new ArgumentException($"Invalid table name: {tableName}");
    }

    public async Task<MigrationPreview> PreviewAsync(string connectionString, string tableName)
    {
        ValidateTableName(tableName);
        var preview = new MigrationPreview();

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        // Count total rows
        await using var countCmd = new SqlCommand($"SELECT COUNT(*) FROM [{tableName}]", conn);
        preview.TotalRows = (int)await countCmd.ExecuteScalarAsync();

        // Read card numbers to check duplicates
        await using var cmd = new SqlCommand(
            $"SELECT UserName, UserCard FROM [{tableName}] ORDER BY UsrId", conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var duplicateCount = 0;
        var newCount = 0;
        var sampleNames = new List<string>();

        while (await reader.ReadAsync())
        {
            var name = reader["UserName"]?.ToString()?.Trim() ?? "";
            var cardNo = reader["UserCard"]?.ToString()?.Trim() ?? "";

            if (!string.IsNullOrEmpty(cardNo))
            {
                var existing = await _employeeRepository.GetByEmployeeCodeAsync(cardNo);
                if (existing != null)
                    duplicateCount++;
                else
                    newCount++;
            }
            else
            {
                newCount++;
            }

            if (sampleNames.Count < 5 && !string.IsNullOrEmpty(name))
                sampleNames.Add(name);
        }

        preview.DuplicateCards = duplicateCount;
        preview.NewPlayers = newCount;
        preview.SampleNames = sampleNames;
        return preview;
    }

    public async Task<MigrationResult> ImportAsync(string connectionString, string tableName,
        IProgress<(int current, int total, string name)>? progress = null)
    {
        ValidateTableName(tableName);
        var result = new MigrationResult();

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        // Count for progress
        await using var countCmd = new SqlCommand($"SELECT COUNT(*) FROM [{tableName}]", conn);
        var total = (int)await countCmd.ExecuteScalarAsync();

        // Read all rows
        await using var cmd = new SqlCommand(
            $"SELECT * FROM [{tableName}] ORDER BY UsrId", conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var current = 0;
        while (await reader.ReadAsync())
        {
            current++;
            var name = GetString(reader, "UserName");
            var cardNo = GetString(reader, "UserCard");

            progress?.Report((current, total, name));

            try
            {
                // Skip if card already exists
                if (!string.IsNullOrEmpty(cardNo))
                {
                    var existing = await _employeeRepository.GetByEmployeeCodeAsync(cardNo);
                    if (existing != null)
                    {
                        result.Skipped++;
                        continue;
                    }
                }

                // Only copy essential fields (name, card, dates, photo)
                // All secondary fields use safe defaults to prevent DB errors
                var employee = new Employee
                {
                    FullNameAr = name,
                    FullNameEn = name,
                    CardNo = cardNo,
                    Phone = $"MIG-{current}",           // unique placeholder (required, unique)
                    PhotoData = GetImageBytes(reader, "UserPicture"),
                    StartDate = GetDate(reader, "TimeBegin"),
                    EndDate = GetDate(reader, "TimeValidaty"),
                    // Secondary fields - safe defaults
                    SubscriptionType = "Migrated",
                    SubscriptionFee = 0,
                    AmountPaid = 0,
                    Height = 0,
                    Weight = 0,
                    Notes = string.Empty,
                    IsFrozen = false,
                    FreezeStartDate = null,
                    MaxVisits = 0,
                    UsedVisits = 0,
                    CardBalance = 0,
                    CreatedAt = DateTime.UtcNow
                };

                await _employeeRepository.AddAsync(employee);
                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                var rootMsg = ex.GetBaseException().Message;
                result.Errors.Add($"{name} ({cardNo}): {rootMsg}");
            }
        }

        return result;
    }

    private static string GetString(IDataReader reader, string column)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString()?.Trim() ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static int GetInt(IDataReader reader, string column)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return 0;
            return Convert.ToInt32(reader.GetValue(ordinal));
        }
        catch { return 0; }
    }

    private static bool GetBool(IDataReader reader, string column)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return false;
            return Convert.ToBoolean(reader.GetValue(ordinal));
        }
        catch { return false; }
    }

    private static DateTime GetDate(IDataReader reader, string column)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return DateTime.UtcNow.Date;
            return Convert.ToDateTime(reader.GetValue(ordinal));
        }
        catch { return DateTime.UtcNow.Date; }
    }

    private static DateTime? GetNullableDate(IDataReader reader, string column)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return null;
            return Convert.ToDateTime(reader.GetValue(ordinal));
        }
        catch { return null; }
    }

    private static DateTime GetDateOrNow(IDataReader reader, string column)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return DateTime.UtcNow;
            return Convert.ToDateTime(reader.GetValue(ordinal));
        }
        catch { return DateTime.UtcNow; }
    }

    private static byte[]? GetImageBytes(IDataReader reader, string column)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return null;
            return (byte[])reader.GetValue(ordinal);
        }
        catch { return null; }
    }
}
