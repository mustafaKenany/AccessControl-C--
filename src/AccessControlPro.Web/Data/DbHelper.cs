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
    ""Height"" DECIMAL(5,1) DEFAULT 0,
    ""Weight"" DECIMAL(5,1) DEFAULT 0,
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

-- Performance indexes
CREATE INDEX IF NOT EXISTS idx_players_isdeleted ON ""Players"" (""IsDeleted"");
CREATE INDEX IF NOT EXISTS idx_players_phone ON ""Players"" (""Phone"");
CREATE INDEX IF NOT EXISTS idx_players_cardno ON ""Players"" (""CardNo"");
CREATE INDEX IF NOT EXISTS idx_events_eventdate ON ""AccessEvents"" (""EventDate"" DESC);
CREATE INDEX IF NOT EXISTS idx_events_doorid ON ""AccessEvents"" (""DoorId"");
CREATE INDEX IF NOT EXISTS idx_transactions_date ON ""Transactions"" (""TransactionDate"" DESC);
CREATE INDEX IF NOT EXISTS idx_auditlogs_timestamp ON ""AuditLogs"" (""Timestamp"" DESC);
CREATE INDEX IF NOT EXISTS idx_users_username ON ""Users"" (""Username"");
CREATE INDEX IF NOT EXISTS idx_deleted_deletedat ON ""DeletedEmployees"" (""DeletedAt"" DESC);
CREATE INDEX IF NOT EXISTS idx_qrpasses_code ON ""QrPasses"" (""Code"");

-- Gyms table migration: add columns if missing
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""SubscriptionPrice"" DECIMAL(18,2) DEFAULT 0;
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""Notes"" TEXT DEFAULT '';
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""LastSyncAt"" TIMESTAMP NULL;
ALTER TABLE ""Gyms"" ADD COLUMN IF NOT EXISTS ""PlayerCount"" INT DEFAULT 0;

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
