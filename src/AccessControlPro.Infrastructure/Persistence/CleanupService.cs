using System.IO;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence;

public class CleanupService : ICleanupService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "cleanup_log.txt");

    public CleanupService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n"); } catch { }
    }

    public async Task<int> CleanupInactivePlayersAsync()
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var cutoff = DateTime.Now.AddMonths(-6);
        int count = 0;

        try
        {
            // Find inactive players (EndDate > 6 months ago, not frozen, not already soft-deleted via IsFrozen trick)
            var inactivePlayers = await db.Employees
                .Where(e => e.EndDate < cutoff && !e.IsFrozen)
                .Select(e => e.Id)
                .ToListAsync();

            if (inactivePlayers.Count == 0) return 0;

            Log($"Found {inactivePlayers.Count} inactive players (expired before {cutoff:yyyy-MM-dd})");

            foreach (var playerId in inactivePlayers)
            {
                try
                {
                    // Step 1: Archive to DeletedEmployees
                    await db.Database.ExecuteSqlRawAsync(@"
                        INSERT INTO DeletedEmployees (OriginalId, FullNameEn, FullNameAr, CardNo, Phone,
                            SubscriptionType, StartDate, EndDate, SubscriptionFee, AmountPaid,
                            PhotoData, Height, Weight, Notes,
                            DeleteReason, DeletedBy, DeletedAt, OriginalCreatedAt)
                        SELECT Id, FullNameEn, FullNameAr, CardNo, Phone,
                            SubscriptionType, StartDate, EndDate, SubscriptionFee, AmountPaid,
                            PhotoData, Height, Weight, Notes,
                            N'Auto-cleanup: no renewal for 6+ months / حذف تلقائي: لم يتم التجديد لأكثر من 6 أشهر',
                            N'System', GETUTCDATE(), CreatedAt
                        FROM Employees WHERE Id = {0}", playerId);

                    // Step 2: Nullify FK in Transactions
                    try
                    {
                        await db.Database.ExecuteSqlRawAsync(
                            "UPDATE Transactions SET RelatedEmployeeId = NULL WHERE RelatedEmployeeId = {0}", playerId);
                    }
                    catch { /* Table may not exist */ }

                    // Step 3: Nullify FK references in AccessEvents
                    try
                    {
                        await db.Database.ExecuteSqlRawAsync(
                            "UPDATE AccessEvents SET CardId = NULL WHERE CardId IN (SELECT Id FROM AccessCards WHERE EmployeeId = {0})", playerId);
                    }
                    catch { /* Table may not exist */ }

                    // Step 4: Delete CardDeviceSyncs
                    try
                    {
                        await db.Database.ExecuteSqlRawAsync(
                            "DELETE FROM CardDeviceSyncs WHERE CardId IN (SELECT Id FROM AccessCards WHERE EmployeeId = {0})", playerId);
                    }
                    catch { /* Table may not exist */ }

                    // Step 5: Delete QrPasses linked to employee's cards
                    try
                    {
                        await db.Database.ExecuteSqlRawAsync(
                            "DELETE FROM QrPasses WHERE CardId IN (SELECT Id FROM AccessCards WHERE EmployeeId = {0})", playerId);
                    }
                    catch { /* Table may not exist */ }

                    // Step 6: Delete AccessCards
                    try
                    {
                        await db.Database.ExecuteSqlRawAsync(
                            "DELETE FROM AccessCards WHERE EmployeeId = {0}", playerId);
                    }
                    catch { /* Table may not exist */ }

                    // Step 7: Delete FreezeHistories
                    try
                    {
                        await db.Database.ExecuteSqlRawAsync(
                            "DELETE FROM FreezeHistories WHERE EmployeeId = {0}", playerId);
                    }
                    catch { /* Table may not exist */ }

                    // Step 8: Delete Employee
                    await db.Database.ExecuteSqlRawAsync(
                        "DELETE FROM Employees WHERE Id = {0}", playerId);

                    count++;
                    Log($"  Archived and deleted player ID {playerId}");
                }
                catch (Exception ex)
                {
                    Log($"  Failed to cleanup player {playerId}: {ex.Message}");
                }
            }

            Log($"Cleanup complete: {count}/{inactivePlayers.Count} inactive players archived and deleted");
        }
        catch (Exception ex)
        {
            Log($"Cleanup inactive players error: {ex.Message}");
        }

        return count;
    }

    public async Task<int> CleanupOldEventsAsync()
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var cutoff = DateTime.Now.AddMonths(-6);
        int count = 0;

        try
        {
            // Delete events older than 6 months
            count = await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM AccessEvents WHERE [Timestamp] < {0}", cutoff);

            if (count > 0)
                Log($"Cleaned up {count} old access events (older than {cutoff:yyyy-MM-dd})");

            // Also cleanup old audit logs (older than 1 year)
            try
            {
                var auditCutoff = DateTime.Now.AddYears(-1);
                var auditCount = await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM AuditLogs WHERE [Timestamp] < {0}", auditCutoff);

                if (auditCount > 0)
                    Log($"Cleaned up {auditCount} old audit logs (older than {auditCutoff:yyyy-MM-dd})");
            }
            catch { /* AuditLogs table may not exist */ }

            // Cleanup QR Pool: delete DB records older than 6 months that are used/expired
            try
            {
                var qrCutoff = DateTime.Now.AddMonths(-6);
                var qrCleanup = await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM QrPool WHERE (Status = 2 OR Status = 3) AND CreatedAt < {0}", qrCutoff);

                if (qrCleanup > 0)
                    Log($"Cleaned up {qrCleanup} old QR pool entries (>6 months)");
            }
            catch { /* QrPool table may not exist */ }
        }
        catch (Exception ex)
        {
            Log($"Cleanup old events error: {ex.Message}");
        }

        return count;
    }
}
