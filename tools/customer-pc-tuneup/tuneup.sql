-- =============================================================================
-- AccessControlPro — One-shot customer-PC tuneup
-- =============================================================================
-- Run once after installing the app on a customer's PC. Safe to re-run.
--
-- Effects:
--   1. Caps SQL Server max memory at 2 GB (leaves 6 GB for WPF + Windows)
--   2. Creates the indexes the app actually hits (events, cards, players,
--      QR pool, audit log, transactions)
--   3. Updates statistics so the query planner picks the right index
--
-- The companion tuneup.bat runs this via sqlcmd and then applies the
-- Windows-level tweaks (Power Plan, Defender exclusion). Run that one if
-- you want the full tuneup in one click.
-- =============================================================================

-- ------------------------------------------------------------------
-- 1. SQL Server memory cap (master DB)
-- ------------------------------------------------------------------
USE master;
GO

EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;

-- Min memory: 512 MB guarantees SQL never gets starved completely
EXEC sp_configure 'min server memory (MB)', 512;

-- Max memory: 2048 MB caps SQL so the OS + WPF have RAM left.
-- On a typical i5 / 8 GB box: 2 GB SQL + 1.5 GB WPF + 2 GB Windows + 2.5 GB headroom.
EXEC sp_configure 'max server memory (MB)', 2048;

RECONFIGURE;
GO

PRINT '== SQL Server memory cap set to 2 GB ==';
GO

-- ------------------------------------------------------------------
-- 2. Indexes on hot tables
-- ------------------------------------------------------------------
-- SQL Server <2016 doesn't have "CREATE INDEX IF NOT EXISTS", so we
-- guard each one explicitly via sys.indexes.
-- ------------------------------------------------------------------
USE AccessControlPro;
GO

-- AccessEvents: timestamp DESC (Events page sort), DoorId/CardId filters
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessEvents_Timestamp' AND object_id = OBJECT_ID('AccessEvents'))
    CREATE NONCLUSTERED INDEX IX_AccessEvents_Timestamp ON AccessEvents([Timestamp] DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessEvents_DoorId' AND object_id = OBJECT_ID('AccessEvents'))
    CREATE NONCLUSTERED INDEX IX_AccessEvents_DoorId ON AccessEvents(DoorId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessEvents_CardId' AND object_id = OBJECT_ID('AccessEvents'))
    CREATE NONCLUSTERED INDEX IX_AccessEvents_CardId ON AccessEvents(CardId);

-- AccessCards: lookup by card number + employee
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessCards_CardNumber' AND object_id = OBJECT_ID('AccessCards'))
    CREATE NONCLUSTERED INDEX IX_AccessCards_CardNumber ON AccessCards(CardNumber);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessCards_EmployeeId' AND object_id = OBJECT_ID('AccessCards'))
    CREATE NONCLUSTERED INDEX IX_AccessCards_EmployeeId ON AccessCards(EmployeeId);

-- Employees (Players): card lookups and phone search
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_CardNo' AND object_id = OBJECT_ID('Employees'))
    CREATE NONCLUSTERED INDEX IX_Employees_CardNo ON Employees(CardNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_Phone' AND object_id = OBJECT_ID('Employees'))
    CREATE NONCLUSTERED INDEX IX_Employees_Phone ON Employees(Phone);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_EndDate' AND object_id = OBJECT_ID('Employees'))
    CREATE NONCLUSTERED INDEX IX_Employees_EndDate ON Employees(EndDate);

-- QR Pool: assignment + device-sync paths
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QrPool_Status_Source' AND object_id = OBJECT_ID('QrPool'))
    CREATE NONCLUSTERED INDEX IX_QrPool_Status_Source ON QrPool([Status], Source) INCLUDE (Code);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QrPool_IsUploadedToDevice' AND object_id = OBJECT_ID('QrPool'))
    CREATE NONCLUSTERED INDEX IX_QrPool_IsUploadedToDevice ON QrPool(IsUploadedToDevice) INCLUDE (Code, [Status]);

-- AuditLogs: timestamp DESC for the Logs page
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_Timestamp' AND object_id = OBJECT_ID('AuditLogs'))
    CREATE NONCLUSTERED INDEX IX_AuditLogs_Timestamp ON AuditLogs([Timestamp] DESC);

-- Transactions: for the Finance page sort + filter
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transactions_CreatedAt' AND object_id = OBJECT_ID('Transactions'))
    CREATE NONCLUSTERED INDEX IX_Transactions_CreatedAt ON Transactions(CreatedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transactions_Type' AND object_id = OBJECT_ID('Transactions'))
    CREATE NONCLUSTERED INDEX IX_Transactions_Type ON Transactions([Type]);

PRINT '== Indexes ensured on hot tables ==';
GO

-- ------------------------------------------------------------------
-- 3. Statistics refresh — so the planner uses the new indexes
-- ------------------------------------------------------------------
EXEC sp_updatestats;
GO

PRINT '== Statistics refreshed ==';
PRINT '';
PRINT '== Tuneup complete ==';
GO
