-- =============================================================================
-- Cloud DB Forensic Audit — gymcloud_drag
-- =============================================================================
-- Date created : 2026-05-13
-- Target DB    : gymcloud_drag (PostgreSQL on Hostinger VPS 89.116.39.155)
-- Purpose      : Find data inconsistencies, logical errors, and migration
--                leftovers in the cloud database.
--
-- HOW TO RUN
-- ----------
-- On the VPS:
--   sudo -u postgres psql gymcloud_drag -f audit.sql > audit_results.txt
-- Or interactively:
--   sudo -u postgres psql gymcloud_drag
--   \i /path/to/audit.sql
--
-- Each section prints a heading, a COUNT, and up to 5 example rows so the
-- output stays manageable even on large tables. If a count is 0 — that
-- section is healthy. If a count is non-zero — investigate further.
-- =============================================================================

\echo '=========================================='
\echo '  CLOUD DB AUDIT  —  gymcloud_drag'
\echo '=========================================='
\echo ''

-- =============================================================================
-- 1. CARDS & PLAYERS
-- =============================================================================
\echo '--- 1. CARDS & PLAYERS ---'

\echo ''
\echo '1a. AccessCards with no Employee linked (orphan cards from format mismatch):'
SELECT COUNT(*) AS orphan_card_count
FROM "AccessCards"
WHERE "EmployeeId" IS NULL;

\echo '    Examples of orphan card numbers:'
SELECT "Id", "CardNumber", "ValidFrom", "ValidTo", "IsActive"
FROM "AccessCards"
WHERE "EmployeeId" IS NULL
ORDER BY "Id" DESC
LIMIT 5;

\echo ''
\echo '1b. Orphan cards that COULD be matched to a Player via /10 rule:'
SELECT COUNT(*) AS recoverable_orphan_count
FROM "AccessCards" c
WHERE c."EmployeeId" IS NULL
  AND c."CardNumber" ~ '^[0-9]+$'
  AND EXISTS (
    SELECT 1 FROM "Players" p
    WHERE p."CardNo" ~ '^[0-9]+$'
      AND p."CardNo"::bigint = c."CardNumber"::bigint / 10
  );

\echo '    Examples of recoverable orphans + their matching players:'
SELECT c."Id" AS orphan_card_id, c."CardNumber" AS scanned_card,
       p."Id" AS player_id, p."CardNo" AS registered_card,
       p."FullNameAr"
FROM "AccessCards" c
JOIN "Players" p ON p."CardNo" ~ '^[0-9]+$'
                AND c."CardNumber" ~ '^[0-9]+$'
                AND p."CardNo"::bigint = c."CardNumber"::bigint / 10
WHERE c."EmployeeId" IS NULL
LIMIT 5;

\echo ''
\echo '1c. Duplicate AccessCard rows (same CardNumber appears more than once):'
SELECT "CardNumber", COUNT(*) AS dup_count
FROM "AccessCards"
GROUP BY "CardNumber"
HAVING COUNT(*) > 1
ORDER BY COUNT(*) DESC
LIMIT 10;

\echo ''
\echo '1d. Two Players with the same CardNo (data corruption):'
SELECT "CardNo", COUNT(*) AS player_count, STRING_AGG("FullNameAr", ', ') AS players
FROM "Players"
WHERE "CardNo" IS NOT NULL AND "CardNo" <> ''
GROUP BY "CardNo"
HAVING COUNT(*) > 1
LIMIT 10;

\echo ''
\echo '1e. Players with no card assigned:'
SELECT COUNT(*) AS players_without_card
FROM "Players" p
WHERE NOT EXISTS (SELECT 1 FROM "AccessCards" c WHERE c."EmployeeId" = p."Id");

\echo ''
\echo '1f. Players with multiple cards (could be legitimate or duplicates):'
SELECT p."Id", p."FullNameAr", COUNT(c."Id") AS card_count
FROM "Players" p
JOIN "AccessCards" c ON c."EmployeeId" = p."Id"
GROUP BY p."Id", p."FullNameAr"
HAVING COUNT(c."Id") > 1
ORDER BY COUNT(c."Id") DESC
LIMIT 10;


-- =============================================================================
-- 2. ACCESS EVENTS
-- =============================================================================
\echo ''
\echo '--- 2. ACCESS EVENTS ---'

\echo ''
\echo '2a. Total events in cloud:'
SELECT COUNT(*) AS total_events FROM "AccessEvents";

\echo ''
\echo '2b. Events with CardId = NULL (unmatched card at time of scan):'
SELECT COUNT(*) AS unmatched_event_count
FROM "AccessEvents"
WHERE "CardId" IS NULL;

\echo ''
\echo '2c. Events with EventDate in the future (clock skew or bad timezone):'
SELECT COUNT(*) AS future_event_count
FROM "AccessEvents"
WHERE "EventDate" > NOW() + INTERVAL '1 day';

\echo '    Examples of future events:'
SELECT "Id", "EventDate", LEFT("Details", 80) AS details
FROM "AccessEvents"
WHERE "EventDate" > NOW() + INTERVAL '1 day'
ORDER BY "EventDate" DESC
LIMIT 5;

\echo ''
\echo '2d. Events with EventDate before 2024 (suspicious old data):'
SELECT COUNT(*) AS ancient_event_count
FROM "AccessEvents"
WHERE "EventDate" < '2024-01-01';

\echo ''
\echo '2e. Events for doors that no longer exist (referential integrity gap):'
SELECT COUNT(*) AS orphan_door_event_count
FROM "AccessEvents" e
WHERE e."DoorId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM "Doors" d WHERE d."Id" = e."DoorId");

\echo ''
\echo '2f. Distribution of event ages (helps spot a sync gap):'
SELECT
  CASE
    WHEN "EventDate" > NOW() - INTERVAL '1 day'  THEN 'last 24h'
    WHEN "EventDate" > NOW() - INTERVAL '7 days' THEN 'last 7d'
    WHEN "EventDate" > NOW() - INTERVAL '30 days' THEN 'last 30d'
    WHEN "EventDate" > NOW() - INTERVAL '90 days' THEN 'last 90d'
    ELSE 'older'
  END AS bucket,
  COUNT(*) AS events
FROM "AccessEvents"
GROUP BY bucket
ORDER BY MIN("EventDate") DESC;


-- =============================================================================
-- 3. PLAYERS / SUBSCRIPTIONS
-- =============================================================================
\echo ''
\echo '--- 3. PLAYERS / SUBSCRIPTIONS ---'

\echo ''
\echo '3a. Total active players in cloud:'
SELECT COUNT(*) AS total_players FROM "Players";

\echo ''
\echo '3b. Players still flagged as Migrated (Phone="MIG-N" or SubscriptionType="Migrated"):'
SELECT COUNT(*) AS still_migrated_count
FROM "Players"
WHERE ("Phone" ~ '^MIG-[0-9]+$' OR "SubscriptionType" = 'Migrated');

\echo '    Examples:'
SELECT "Id", "FullNameAr", "Phone", "SubscriptionType", "SubscriptionFee"
FROM "Players"
WHERE ("Phone" ~ '^MIG-[0-9]+$' OR "SubscriptionType" = 'Migrated')
LIMIT 5;

\echo ''
\echo '3c. Players with EndDate < StartDate (invalid date range):'
SELECT COUNT(*) AS invalid_date_range_count
FROM "Players"
WHERE "EndDate" < "StartDate";

\echo ''
\echo '3d. Players with EndDate in past but not flagged Frozen or Expired in any way:'
SELECT COUNT(*) AS expired_but_active_count
FROM "Players"
WHERE "EndDate" < NOW()
  AND NOT COALESCE("IsFrozen", false);

\echo ''
\echo '3e. Players with SubscriptionFee = 0 (free or unpaid):'
SELECT COUNT(*) AS zero_fee_count
FROM "Players"
WHERE "SubscriptionFee" = 0;

\echo ''
\echo '3f. Players where AmountPaid > SubscriptionFee (overpaid):'
SELECT COUNT(*) AS overpaid_count
FROM "Players"
WHERE COALESCE("AmountPaid", 0) > COALESCE("SubscriptionFee", 0);

\echo ''
\echo '3g. SubscriptionTypes in use that DON''T exist in SubscriptionPlans table:'
SELECT DISTINCT "SubscriptionType", COUNT(*) AS player_count
FROM "Players"
WHERE "SubscriptionType" IS NOT NULL
  AND "SubscriptionType" <> ''
  AND NOT EXISTS (
    SELECT 1 FROM "SubscriptionPlans" sp WHERE sp."NameEn" = "SubscriptionType"
  )
GROUP BY "SubscriptionType"
ORDER BY COUNT(*) DESC
LIMIT 10;


-- =============================================================================
-- 4. FINANCE
-- =============================================================================
\echo ''
\echo '--- 4. FINANCE / TRANSACTIONS ---'

\echo ''
\echo '4a. Total transactions:'
SELECT COUNT(*) AS total_transactions FROM "Transactions";

\echo ''
\echo '4b. Transactions with NULL or empty Category:'
SELECT COUNT(*) AS uncategorised_count
FROM "Transactions"
WHERE "Category" IS NULL OR "Category" = '';

\echo ''
\echo '4c. Transactions with Amount = 0 or negative (suspicious):'
SELECT COUNT(*) AS zero_amount_count
FROM "Transactions"
WHERE "Amount" <= 0;

\echo ''
\echo '4d. Transactions with TransactionDate in the future:'
SELECT COUNT(*) AS future_txn_count
FROM "Transactions"
WHERE "TransactionDate" > NOW() + INTERVAL '1 day';


-- =============================================================================
-- 5. QR POOL
-- =============================================================================
\echo ''
\echo '--- 5. QR POOL ---'

\echo ''
\echo '5a. QR Pool stats by status:'
SELECT "Status",
       CASE "Status"
         WHEN 0 THEN 'Available'
         WHEN 1 THEN 'Assigned'
         WHEN 2 THEN 'Used'
         WHEN 3 THEN 'Expired'
         ELSE 'Unknown'
       END AS status_name,
       COUNT(*) AS count
FROM "QrPool"
GROUP BY "Status"
ORDER BY "Status";

\echo ''
\echo '5b. Assigned QR codes with NULL AssignedAt (logic error):'
SELECT COUNT(*) AS assigned_no_timestamp_count
FROM "QrPool"
WHERE "Status" = 1 AND "AssignedAt" IS NULL;

\echo ''
\echo '5c. QrPool entries where ValidTo < ValidFrom (invalid window):'
SELECT COUNT(*) AS invalid_window_count
FROM "QrPool"
WHERE "ValidTo" < "ValidFrom";

\echo ''
\echo '5d. QrPool entries where ValidTo < NOW() but Status still = 1 (should be Expired):'
SELECT COUNT(*) AS stale_assigned_count
FROM "QrPool"
WHERE "Status" = 1 AND "ValidTo" < NOW();


-- =============================================================================
-- 6. CLOUD SYNC HEALTH
-- =============================================================================
\echo ''
\echo '--- 6. CLOUD SYNC HEALTH ---'

\echo ''
\echo '6a. Sync attempts in last 7 days by status:'
SELECT "Status", COUNT(*) AS sync_count
FROM "CloudSyncLogs"
WHERE "SyncedAt" > NOW() - INTERVAL '7 days'
GROUP BY "Status"
ORDER BY COUNT(*) DESC;

\echo ''
\echo '6b. Last 5 failed syncs (if any):'
SELECT "Id", "Status", LEFT("Details", 150) AS details, "SyncedAt"
FROM "CloudSyncLogs"
WHERE "Status" NOT IN ('Success', 'success')
ORDER BY "SyncedAt" DESC
LIMIT 5;

\echo ''
\echo '6c. Gap detection — longest period without ANY sync in last 30 days:'
WITH ranked AS (
  SELECT "SyncedAt",
         LAG("SyncedAt") OVER (ORDER BY "SyncedAt") AS prev_sync,
         "SyncedAt" - LAG("SyncedAt") OVER (ORDER BY "SyncedAt") AS gap
  FROM "CloudSyncLogs"
  WHERE "SyncedAt" > NOW() - INTERVAL '30 days'
)
SELECT prev_sync AS gap_start, "SyncedAt" AS gap_end, gap
FROM ranked
WHERE gap > INTERVAL '30 minutes'
ORDER BY gap DESC
LIMIT 5;


-- =============================================================================
-- 7. AUDIT LOGS
-- =============================================================================
\echo ''
\echo '--- 7. AUDIT LOGS ---'

\echo ''
\echo '7a. Total audit log entries:'
SELECT COUNT(*) AS total_audit_logs FROM "AuditLogs";

\echo ''
\echo '7b. Most frequent actions in audit log:'
SELECT "Action", COUNT(*) AS action_count
FROM "AuditLogs"
GROUP BY "Action"
ORDER BY COUNT(*) DESC
LIMIT 10;

\echo ''
\echo '7c. Audit log entries with NULL PerformedBy (unknown user actions):'
SELECT COUNT(*) AS anonymous_action_count
FROM "AuditLogs"
WHERE "PerformedBy" IS NULL OR "PerformedBy" = '';


-- =============================================================================
-- 8. REFERENTIAL INTEGRITY SPOT CHECKS
-- =============================================================================
\echo ''
\echo '--- 8. REFERENTIAL INTEGRITY ---'

\echo ''
\echo '8a. AccessCards referencing non-existent Player:'
SELECT COUNT(*) AS dangling_card_employee_ref
FROM "AccessCards" c
WHERE c."EmployeeId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM "Players" p WHERE p."Id" = c."EmployeeId");

\echo ''
\echo '8b. Transactions referencing non-existent Player:'
SELECT COUNT(*) AS dangling_txn_employee_ref
FROM "Transactions" t
WHERE t."RelatedEmployeeId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM "Players" p WHERE p."Id" = t."RelatedEmployeeId");

\echo ''
\echo '8c. DeletedEmployees with no OriginalId (lost tombstone):'
SELECT COUNT(*) AS lost_tombstone_count
FROM "DeletedEmployees"
WHERE "OriginalId" IS NULL OR "OriginalId" = 0;


\echo ''
\echo '=========================================='
\echo '  AUDIT COMPLETE'
\echo '=========================================='
\echo ''
\echo 'Send the entire output back to Claude for analysis.'
