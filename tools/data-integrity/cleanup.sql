-- =============================================================================
-- AccessControlPro — Data integrity cleanup
-- =============================================================================
-- Run once per customer DB after install to clean up legacy garbage that has
-- built up from imports, app crashes, and duplicate clicks. Safe to re-run;
-- every step is idempotent.
--
-- Effects:
--   1. Dangling AccessCard rows  — cards pointing to deleted players (32 on Basmia)
--   2. Duplicate AccessCard rows — same CardNumber on multiple rows, keep newest
--   3. Empty-string CardNumber rows — junk from old crashed-mid-add operations
--   4. Orphaned FreezeHistory rows — freeze records for deleted players
--   5. Orphaned Transactions — RelatedEmployeeId pointing to deleted players
--
-- How to run:
--   cleanup.bat                  (preferred — admin elevation + reports)
--   OR via SSMS:                 open this file, Execute
--   OR via sqlcmd:               sqlcmd -S localhost -U sa -P 123 -d AccessControlPro -i cleanup.sql
--
-- WHAT IT DOES NOT DO:
--   - Does NOT delete the 1,853 "Migrated" player rows on Basmia. Those carry
--     real card numbers and partial history; deleting them would lock real
--     gym members out. They get cleaned naturally as the receptionist renews
--     each one (the renewal flow forces re-entry of phone/DOB).
-- =============================================================================

USE AccessControlPro;
SET NOCOUNT ON;
GO

PRINT '';
PRINT '============================================================';
PRINT ' AccessControlPro - Data Integrity Cleanup';
PRINT ' Started:  ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '============================================================';
PRINT '';

-- ------------------------------------------------------------------
-- Audit snapshot — what we have BEFORE the cleanup
-- ------------------------------------------------------------------
DECLARE @PlayersTotal INT       = (SELECT COUNT(*) FROM Employees);
DECLARE @PlayersMigrated INT    = (SELECT COUNT(*) FROM Employees WHERE FullNameEn LIKE '%Migrated%' OR FullNameAr LIKE '%مهاجر%');
DECLARE @CardsTotal INT         = (SELECT COUNT(*) FROM AccessCards);
DECLARE @CardsDangling INT      = (SELECT COUNT(*) FROM AccessCards c LEFT JOIN Employees e ON c.EmployeeId = e.Id WHERE c.EmployeeId <> 0 AND e.Id IS NULL);
DECLARE @CardsDuplicate INT     = (SELECT COUNT(*) - COUNT(DISTINCT CardNumber) FROM AccessCards WHERE CardNumber <> '');
DECLARE @CardsEmpty INT         = (SELECT COUNT(*) FROM AccessCards WHERE CardNumber = '' OR CardNumber IS NULL);
DECLARE @FreezeOrphan INT       = (SELECT COUNT(*) FROM FreezeHistories f LEFT JOIN Employees e ON f.EmployeeId = e.Id WHERE e.Id IS NULL);
DECLARE @TransactionOrphan INT  = (SELECT COUNT(*) FROM Transactions t LEFT JOIN Employees e ON t.RelatedEmployeeId = e.Id WHERE t.RelatedEmployeeId IS NOT NULL AND t.RelatedEmployeeId <> 0 AND e.Id IS NULL);

PRINT 'Before cleanup:';
PRINT '  Players total:           ' + CAST(@PlayersTotal AS VARCHAR);
PRINT '  Players migrated:        ' + CAST(@PlayersMigrated AS VARCHAR) + ' (will NOT be touched)';
PRINT '  AccessCards total:       ' + CAST(@CardsTotal AS VARCHAR);
PRINT '  Dangling cards:          ' + CAST(@CardsDangling AS VARCHAR);
PRINT '  Duplicate-number cards:  ' + CAST(@CardsDuplicate AS VARCHAR);
PRINT '  Empty card numbers:      ' + CAST(@CardsEmpty AS VARCHAR);
PRINT '  Orphan FreezeHistories:  ' + CAST(@FreezeOrphan AS VARCHAR);
PRINT '  Orphan Transactions:     ' + CAST(@TransactionOrphan AS VARCHAR);
PRINT '';
GO

-- ------------------------------------------------------------------
-- 1. Dangling AccessCard rows (EmployeeId points to deleted player)
-- ------------------------------------------------------------------
PRINT 'Step 1: cleaning dangling AccessCard rows...';
DELETE c
FROM AccessCards c
LEFT JOIN Employees e ON c.EmployeeId = e.Id
WHERE c.EmployeeId <> 0
  AND e.Id IS NULL;
PRINT '  ' + CAST(@@ROWCOUNT AS VARCHAR) + ' dangling card row(s) deleted';
GO

-- ------------------------------------------------------------------
-- 2. Duplicate AccessCard rows — keep the most-recently-created row,
--    delete the older duplicates for the same CardNumber.
-- ------------------------------------------------------------------
PRINT 'Step 2: resolving duplicate CardNumber rows...';
;WITH Ranked AS (
    SELECT
        Id,
        ROW_NUMBER() OVER (PARTITION BY CardNumber ORDER BY CreatedAt DESC, Id DESC) AS rn
    FROM AccessCards
    WHERE CardNumber <> '' AND CardNumber IS NOT NULL
)
DELETE FROM Ranked WHERE rn > 1;
PRINT '  ' + CAST(@@ROWCOUNT AS VARCHAR) + ' duplicate card row(s) deleted';
GO

-- ------------------------------------------------------------------
-- 3. Empty CardNumber rows — never produced any actual card
-- ------------------------------------------------------------------
PRINT 'Step 3: removing AccessCard rows with empty CardNumber...';
DELETE FROM AccessCards
WHERE CardNumber = '' OR CardNumber IS NULL;
PRINT '  ' + CAST(@@ROWCOUNT AS VARCHAR) + ' empty-number card row(s) deleted';
GO

-- ------------------------------------------------------------------
-- 4. Orphaned FreezeHistory rows
-- ------------------------------------------------------------------
PRINT 'Step 4: cleaning orphaned FreezeHistory rows...';
DELETE f
FROM FreezeHistories f
LEFT JOIN Employees e ON f.EmployeeId = e.Id
WHERE e.Id IS NULL;
PRINT '  ' + CAST(@@ROWCOUNT AS VARCHAR) + ' orphan freeze row(s) deleted';
GO

-- ------------------------------------------------------------------
-- 5. Orphaned Transaction rows (NULL out the FK rather than delete —
--    transactions are accounting records, must NEVER be deleted; just
--    detach them from the missing player so they show as "(deleted player)").
-- ------------------------------------------------------------------
PRINT 'Step 5: detaching orphaned Transaction.RelatedEmployeeId references...';
UPDATE t
SET t.RelatedEmployeeId = NULL
FROM Transactions t
LEFT JOIN Employees e ON t.RelatedEmployeeId = e.Id
WHERE t.RelatedEmployeeId IS NOT NULL
  AND t.RelatedEmployeeId <> 0
  AND e.Id IS NULL;
PRINT '  ' + CAST(@@ROWCOUNT AS VARCHAR) + ' transaction row(s) detached';
GO

-- ------------------------------------------------------------------
-- Audit snapshot — what we have AFTER the cleanup
-- ------------------------------------------------------------------
DECLARE @CardsAfter INT          = (SELECT COUNT(*) FROM AccessCards);
DECLARE @CardsDanglingAfter INT  = (SELECT COUNT(*) FROM AccessCards c LEFT JOIN Employees e ON c.EmployeeId = e.Id WHERE c.EmployeeId <> 0 AND e.Id IS NULL);
DECLARE @CardsDupAfter INT       = (SELECT COUNT(*) - COUNT(DISTINCT CardNumber) FROM AccessCards WHERE CardNumber <> '');
DECLARE @CardsEmptyAfter INT     = (SELECT COUNT(*) FROM AccessCards WHERE CardNumber = '' OR CardNumber IS NULL);
DECLARE @FreezeAfter INT         = (SELECT COUNT(*) FROM FreezeHistories f LEFT JOIN Employees e ON f.EmployeeId = e.Id WHERE e.Id IS NULL);

PRINT '';
PRINT '------------------------------------------------------------';
PRINT 'After cleanup:';
PRINT '  AccessCards total:       ' + CAST(@CardsAfter AS VARCHAR);
PRINT '  Dangling cards:          ' + CAST(@CardsDanglingAfter AS VARCHAR) + ' (expected 0)';
PRINT '  Duplicate-number cards:  ' + CAST(@CardsDupAfter AS VARCHAR) + ' (expected 0)';
PRINT '  Empty card numbers:      ' + CAST(@CardsEmptyAfter AS VARCHAR) + ' (expected 0)';
PRINT '  Orphan FreezeHistories:  ' + CAST(@FreezeAfter AS VARCHAR) + ' (expected 0)';
PRINT '------------------------------------------------------------';
PRINT 'Cleanup complete: ' + CONVERT(VARCHAR, GETDATE(), 120);
GO
