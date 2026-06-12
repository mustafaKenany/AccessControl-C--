using Npgsql;

namespace AccessControlPro.Web.Data;

public class DbHelper
{
    private readonly string _connectionString;

    public DbHelper(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<NpgsqlConnection> GetConnectionAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task InitializeDatabaseAsync()
    {
        using var conn = await GetConnectionAsync();
        var sql = @"
CREATE TABLE IF NOT EXISTS ""Players"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""FullNameEn"" VARCHAR(200) NOT NULL DEFAULT '',
    ""FullNameAr"" VARCHAR(200) NOT NULL DEFAULT '',
    ""CardNo"" VARCHAR(50) DEFAULT '',
    ""Phone"" VARCHAR(50) DEFAULT '',
    ""SubscriptionType"" VARCHAR(100) DEFAULT '',
    ""StartDate"" TIMESTAMP NOT NULL DEFAULT NOW(),
    ""EndDate"" TIMESTAMP NOT NULL DEFAULT NOW(),
    ""SubscriptionFee"" DECIMAL(18,2) DEFAULT 0,
    ""AmountPaid"" DECIMAL(18,2) DEFAULT 0,
    ""MaxVisits"" INT DEFAULT 0,
    ""UsedVisits"" INT DEFAULT 0,
    ""IsFrozen"" BOOLEAN DEFAULT FALSE,
    ""FreezeStartDate"" TIMESTAMP NULL,
    ""IsDeleted"" BOOLEAN DEFAULT FALSE,
    ""CreatedAt"" TIMESTAMP DEFAULT NOW(),
    ""PhotoPath"" VARCHAR(500) DEFAULT '',
    ""Height"" DECIMAL(10,2) DEFAULT 0,
    ""Weight"" DECIMAL(10,2) DEFAULT 0,
    ""Notes"" TEXT DEFAULT ''
);

CREATE TABLE IF NOT EXISTS ""Users"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Username"" VARCHAR(100) NOT NULL,
    ""PasswordHash"" VARCHAR(500) NOT NULL,
    ""DisplayName"" VARCHAR(200) DEFAULT '',
    ""Role"" VARCHAR(50) DEFAULT 'User',
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""Permissions"" TEXT DEFAULT ''
);

CREATE TABLE IF NOT EXISTS ""Devices"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Name"" VARCHAR(200) DEFAULT '',
    ""SerialNumber"" VARCHAR(100) DEFAULT '',
    ""IP"" VARCHAR(50) DEFAULT '',
    ""MAC"" VARCHAR(50) DEFAULT ''
);

CREATE TABLE IF NOT EXISTS ""Doors"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""DeviceId"" INT DEFAULT 0,
    ""Name"" VARCHAR(200) DEFAULT '',
    ""DoorNumber"" INT DEFAULT 1
);

CREATE TABLE IF NOT EXISTS ""AccessEvents"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""DoorId"" INT DEFAULT 0,
    ""CardId"" INT NULL,
    ""RecordType"" INT DEFAULT 0,
    ""EventCode"" INT DEFAULT 0,
    ""EventDate"" TIMESTAMP DEFAULT NOW(),
    ""Details"" TEXT DEFAULT ''
);

CREATE TABLE IF NOT EXISTS ""Transactions"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Type"" INT DEFAULT 0,
    ""Category"" VARCHAR(100) DEFAULT '',
    ""Amount"" DECIMAL(18,2) DEFAULT 0,
    ""Description"" TEXT DEFAULT '',
    ""RelatedEmployeeId"" INT NULL,
    ""TransactionDate"" TIMESTAMP DEFAULT NOW(),
    ""RecordedBy"" VARCHAR(100) DEFAULT ''
);

CREATE TABLE IF NOT EXISTS ""AppSettings"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""GymName"" VARCHAR(200) DEFAULT ''
);

CREATE TABLE IF NOT EXISTS ""AuditLogs"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Action"" VARCHAR(100) DEFAULT '',
    ""EntityType"" VARCHAR(100) DEFAULT '',
    ""EntityId"" INT DEFAULT 0,
    ""Details"" TEXT DEFAULT '',
    ""DetailsAr"" TEXT DEFAULT '',
    ""PerformedBy"" VARCHAR(100) DEFAULT '',
    ""Timestamp"" TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ""DeletedEmployees"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""OriginalId"" INT DEFAULT 0,
    ""FullNameEn"" VARCHAR(200) DEFAULT '',
    ""FullNameAr"" VARCHAR(200) DEFAULT '',
    ""CardNo"" VARCHAR(50) DEFAULT '',
    ""Phone"" VARCHAR(50) DEFAULT '',
    ""DeleteReason"" TEXT DEFAULT '',
    ""DeletedBy"" VARCHAR(100) DEFAULT '',
    ""DeletedAt"" TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ""CloudSyncLogs"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""SyncType"" VARCHAR(100) DEFAULT '',
    ""Status"" VARCHAR(50) DEFAULT '',
    ""Details"" TEXT DEFAULT '',
    ""SyncedAt"" TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ""QrPasses"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Code"" VARCHAR(100) NOT NULL,
    ""PlayerName"" VARCHAR(200) DEFAULT '',
    ""MaxUses"" INT DEFAULT 1,
    ""UsedCount"" INT DEFAULT 0,
    ""ValidFrom"" TIMESTAMP DEFAULT NOW(),
    ""ValidTo"" TIMESTAMP DEFAULT NOW(),
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""CreatedBy"" VARCHAR(100) DEFAULT '',
    ""CreatedAt"" TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ""QrPool"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Code"" VARCHAR(20) NOT NULL,
    ""Status"" INT DEFAULT 0,
    ""Source"" VARCHAR(10) DEFAULT 'Cloud',
    ""GuestName"" VARCHAR(200) DEFAULT '',
    ""GuestPhone"" VARCHAR(50) DEFAULT '',
    ""Reason"" VARCHAR(500) DEFAULT '',
    ""AssignedAt"" TIMESTAMP NULL,
    ""UsedAt"" TIMESTAMP NULL,
    ""ExpiredAt"" TIMESTAMP NULL,
    ""MaxUses"" INT DEFAULT 2,
    ""UsedCount"" INT DEFAULT 0,
    ""ValidFrom"" TIMESTAMP DEFAULT NOW(),
    ""ValidTo"" TIMESTAMP DEFAULT (NOW() + INTERVAL '1 year'),
    ""DoorPermissions"" VARCHAR(20) DEFAULT '01010000',
    ""CreatedAt"" TIMESTAMP DEFAULT NOW(),
    ""IsUploadedToDevice"" BOOLEAN DEFAULT FALSE
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_qrpool_code ON ""QrPool"" (""Code"");

CREATE TABLE IF NOT EXISTS ""AccessCards"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""EmployeeId"" INT DEFAULT 0,
    ""CardNumber"" VARCHAR(50) DEFAULT '',
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""ValidFrom"" TIMESTAMP DEFAULT NOW(),
    ""ValidTo"" TIMESTAMP DEFAULT NOW(),
    ""EffectiveTimes"" INT DEFAULT 65535,
    ""CreatedAt"" TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ""Gyms"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Name"" VARCHAR(200) NOT NULL DEFAULT '',
    ""Subdomain"" VARCHAR(100) NOT NULL DEFAULT '',
    ""ApiKey"" VARCHAR(100) NOT NULL DEFAULT '',
    ""DatabaseName"" VARCHAR(100) NOT NULL DEFAULT '',
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""ExpiresAt"" TIMESTAMP DEFAULT (NOW() + INTERVAL '1 year'),
    ""CreatedAt"" TIMESTAMP DEFAULT NOW(),
    ""OwnerName"" VARCHAR(200) DEFAULT '',
    ""OwnerPhone"" VARCHAR(50) DEFAULT '',
    ""OwnerEmail"" VARCHAR(200) DEFAULT '',
    ""SubscriptionPrice"" DECIMAL(18,2) DEFAULT 0,
    ""Notes"" TEXT DEFAULT '',
    ""LastSyncAt"" TIMESTAMP NULL,
    ""PlayerCount"" INT DEFAULT 0
);

CREATE TABLE IF NOT EXISTS ""DiagnosticsUploads"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""GymId"" INT NOT NULL DEFAULT 0,
    ""FileName"" VARCHAR(200) DEFAULT '',
    ""FilePath"" TEXT DEFAULT '',
    ""FileSizeBytes"" BIGINT DEFAULT 0,
    ""AppVersion"" VARCHAR(50) DEFAULT '',
    ""Trigger"" VARCHAR(20) DEFAULT 'manual',
    ""UploadedAt"" TIMESTAMP DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_diagnostics_gym_time ON ""DiagnosticsUploads"" (""GymId"", ""UploadedAt"" DESC);

CREATE TABLE IF NOT EXISTS ""SubscriptionPlans"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""NameEn"" VARCHAR(200) NOT NULL DEFAULT '',
    ""NameAr"" VARCHAR(200) DEFAULT '',
    ""Duration"" INT DEFAULT 30,
    ""DurationType"" VARCHAR(20) DEFAULT 'Days',
    ""Price"" DECIMAL(18,2) DEFAULT 0,
    ""MaxVisits"" INT DEFAULT 0,
    ""EffectiveTimes"" INT DEFAULT 65535,
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""SortOrder"" INT DEFAULT 0,
    ""CreatedAt"" TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ""PosShifts"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""OpenedBy"" VARCHAR(100) DEFAULT '',
    ""OpenedAt"" TIMESTAMP DEFAULT NOW(),
    ""ClosedAt"" TIMESTAMP NULL,
    ""OpeningCash"" DECIMAL(18,2) DEFAULT 0,
    ""ClosingCash"" DECIMAL(18,2) DEFAULT 0,
    ""TotalSales"" DECIMAL(18,2) DEFAULT 0,
    ""TotalCashSales"" DECIMAL(18,2) DEFAULT 0,
    ""TotalCardSales"" DECIMAL(18,2) DEFAULT 0,
    ""Variance"" DECIMAL(18,2) DEFAULT 0,
    ""Status"" VARCHAR(20) DEFAULT 'Open'
);

CREATE TABLE IF NOT EXISTS ""FreezeHistories"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""EmployeeId"" INT DEFAULT 0,
    ""FreezeStart"" TIMESTAMP NULL,
    ""FreezeEnd"" TIMESTAMP NULL,
    ""FreezeDays"" INT DEFAULT 0,
    ""Reason"" VARCHAR(500) DEFAULT '',
    ""CreatedAt"" TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ""Products"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Name"" VARCHAR(200) DEFAULT '',
    ""NameAr"" VARCHAR(200) DEFAULT '',
    ""Price"" DECIMAL(18,2) DEFAULT 0,
    ""Stock"" INT DEFAULT 0,
    ""Barcode"" VARCHAR(100) DEFAULT '',
    ""Category"" VARCHAR(200) DEFAULT '',
    ""IsActive"" BOOLEAN DEFAULT TRUE,
    ""CreatedAt"" TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ""TimeGroups"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""NameEn"" VARCHAR(200) DEFAULT '',
    ""NameAr"" VARCHAR(200) DEFAULT '',
    ""HardwareIndex"" INT DEFAULT 1,
    ""IsDefault"" BOOLEAN DEFAULT FALSE,
    ""ScheduleJson"" TEXT DEFAULT '{}',
    ""CreatedAt"" TIMESTAMP DEFAULT NOW()
);

-- Performance indexes
CREATE INDEX IF NOT EXISTS idx_players_isdeleted ON ""Players"" (""IsDeleted"");
CREATE INDEX IF NOT EXISTS idx_players_phone ON ""Players"" (""Phone"");
CREATE INDEX IF NOT EXISTS idx_players_cardno ON ""Players"" (""CardNo"");
CREATE INDEX IF NOT EXISTS idx_events_eventdate ON ""AccessEvents"" (""EventDate"" DESC);
CREATE INDEX IF NOT EXISTS idx_events_doorid ON ""AccessEvents"" (""DoorId"");
CREATE INDEX IF NOT EXISTS idx_transactions_date ON ""Transactions"" (""TransactionDate"" DESC);
CREATE INDEX IF NOT EXISTS idx_auditlogs_timestamp ON ""AuditLogs"" (""Timestamp"" DESC);
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_username ON ""Users"" (""Username"");
CREATE INDEX IF NOT EXISTS idx_deleted_deletedat ON ""DeletedEmployees"" (""DeletedAt"" DESC);
CREATE INDEX IF NOT EXISTS idx_qrpasses_code ON ""QrPasses"" (""Code"");
CREATE INDEX IF NOT EXISTS idx_freezehistories_employeeid ON ""FreezeHistories"" (""EmployeeId"");
CREATE INDEX IF NOT EXISTS idx_products_isactive ON ""Products"" (""IsActive"");
CREATE INDEX IF NOT EXISTS idx_timegroups_name ON ""TimeGroups"" (""NameEn"");
CREATE INDEX IF NOT EXISTS idx_qrpool_status ON ""QrPool"" (""Status"");
CREATE INDEX IF NOT EXISTS idx_posshifts_status ON ""PosShifts"" (""Status"");
CREATE INDEX IF NOT EXISTS idx_subscriptionplans_isactive ON ""SubscriptionPlans"" (""IsActive"");

-- Sessions: server-side session store. Replaces the plaintext .active_session flat
-- file and JS-set cookies. The cookie sent to the browser is only the random token;
-- all session data lives here and can be revoked atomically (logout, admin action).
CREATE TABLE IF NOT EXISTS ""Sessions"" (
    ""Token"" VARCHAR(128) PRIMARY KEY,
    ""Role"" VARCHAR(50) NOT NULL,
    ""DisplayName"" VARCHAR(200) NOT NULL DEFAULT '',
    ""UserId"" INT NOT NULL DEFAULT 0,
    ""GymDatabase"" VARCHAR(100) NOT NULL DEFAULT '',
    ""GymId"" INT NOT NULL DEFAULT 0,
    ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
    ""ExpiresAt"" TIMESTAMP NOT NULL,
    ""RevokedAt"" TIMESTAMP NULL,
    ""LastSeenAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
    ""LastIp"" VARCHAR(45) DEFAULT '',
    ""LastUserAgent"" VARCHAR(500) DEFAULT ''
);
CREATE INDEX IF NOT EXISTS idx_sessions_expiresat ON ""Sessions"" (""ExpiresAt"");
CREATE INDEX IF NOT EXISTS idx_sessions_userid_gymid ON ""Sessions"" (""UserId"", ""GymId"");

-- AccessCards FK: ensure every card points to a real player. Adds FK if missing.
-- ON DELETE CASCADE so deleting a player auto-removes their cards (mirrors local SQL Server).
-- Wrapped in DO block + EXCEPTION so it skips silently if FK already exists (idempotent migration).
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_name = 'fk_accesscards_employeeid'
          AND table_name = 'AccessCards'
    ) THEN
        ALTER TABLE ""AccessCards""
        ADD CONSTRAINT fk_accesscards_employeeid
        FOREIGN KEY (""EmployeeId"") REFERENCES ""Players""(""Id"")
        ON DELETE CASCADE;
    END IF;
EXCEPTION WHEN OTHERS THEN
    -- FK creation can fail if there are orphan rows; log and continue rather than break startup.
    RAISE NOTICE 'AccessCards FK setup skipped: %', SQLERRM;
END $$;

-- Players migration: widen Height/Weight from DECIMAL(5,1) to DECIMAL(10,2) so
-- values above 999.9 (bad client data, e.g. phone numbers typed into height) don't cause 22003 overflow.
ALTER TABLE ""Players"" ALTER COLUMN ""Height"" TYPE DECIMAL(10,2);
ALTER TABLE ""Players"" ALTER COLUMN ""Weight"" TYPE DECIMAL(10,2);

-- Gyms table migration: add columns if missing
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""SubscriptionPrice"" DECIMAL(18,2) DEFAULT 0;
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""Notes"" TEXT DEFAULT '';
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""LastSyncAt"" TIMESTAMP NULL;
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""PlayerCount"" INT DEFAULT 0;
-- Admin-triggered recovery flag: when true, the next client sync resets its local
-- delta-sync state and sends a full sync (use when client and cloud have drifted).
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""ForceFullSync"" BOOLEAN DEFAULT FALSE;
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""QrPoolEnabled"" BOOLEAN DEFAULT FALSE;
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""QrPoolSize"" INT DEFAULT 0;
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""QrRangeStart"" INT DEFAULT 0;
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""QrMonthlyFee"" DECIMAL(18,2) DEFAULT 0;
-- Remote lock (payment enforcement): when IsLocked = true the desktop app shows a
-- full-screen block with LockMessage and can't be used until the admin unlocks. The app
-- also auto-locks if it can't confirm 'unlocked' with the cloud for 7+ days (anti-bypass).
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""IsLocked"" BOOLEAN DEFAULT FALSE;
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""LockMessage"" TEXT DEFAULT '';

-- Seed default gym if none exists
INSERT INTO ""Gyms"" (""Name"", ""Subdomain"", ""ApiKey"", ""DatabaseName"", ""IsActive"", ""ExpiresAt"", ""OwnerName"")
SELECT 'Default Gym', 'main', 'HMTech-Sync-2026', 'gymcloud', TRUE, '2027-03-21', 'Admin'
WHERE NOT EXISTS (SELECT 1 FROM ""Gyms"");

-- Migration: ensure existing gyms have a DatabaseName
UPDATE ""Gyms"" SET ""DatabaseName"" = 'gymcloud' WHERE ""DatabaseName"" = '' OR ""DatabaseName"" IS NULL;
";
        using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
