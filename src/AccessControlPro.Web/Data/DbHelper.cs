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
";
        using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
