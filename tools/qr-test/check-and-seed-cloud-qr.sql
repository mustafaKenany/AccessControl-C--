-- =============================================================================
-- QR Pool — diagnose + seed cloud-source codes for two-way sync
-- =============================================================================
-- Purpose: cloud "Create QR Pass" pulls from QrPool WHERE Status=0 AND
--          Source='Cloud'. The WPF main app generates its codes with
--          Source='Local'. If no Source='Cloud' codes exist, creating a
--          QR pass on hmtech.solutions fails with "No QR codes available".
--
-- This script:
--   1. Counts QrPool by Source (so we see what's actually there)
--   2. Seeds 2000 fresh Source='Cloud' codes if the count is 0
--
-- Run on the customer's gym DB:
--   sudo -u postgres psql gymcloud_drag -f /tmp/check-and-seed-cloud-qr.sql
-- =============================================================================

\echo '=== Step 1: current QrPool by Source ==='
SELECT "Source", "Status",
       CASE "Status"
         WHEN 0 THEN 'Available'
         WHEN 1 THEN 'Assigned'
         WHEN 2 THEN 'Used'
         WHEN 3 THEN 'Expired'
         ELSE 'Unknown'
       END AS status_name,
       COUNT(*) AS count
FROM "QrPool"
GROUP BY "Source", "Status"
ORDER BY "Source", "Status";

\echo ''
\echo '=== Step 2: seed 2000 cloud codes (60003501..60005500) if none exist ==='
-- Pick a non-overlapping range. Local pool was 50001001-..., so cloud starts
-- at 60000000+ to avoid any chance of collision. Codes are pure numeric so the
-- device's 8H10D reader can still process them.
DO $$
DECLARE
    existing INTEGER;
    seed_count INTEGER := 2000;
    start_from INTEGER := 60003501;
    i INTEGER;
BEGIN
    SELECT COUNT(*) INTO existing
    FROM "QrPool"
    WHERE "Source" = 'Cloud';

    IF existing > 0 THEN
        RAISE NOTICE '% cloud codes already exist — skipping seed.', existing;
    ELSE
        RAISE NOTICE 'Seeding % cloud codes starting from %', seed_count, start_from;
        FOR i IN 0 .. (seed_count - 1) LOOP
            INSERT INTO "QrPool" (
                "Code", "Status", "Source",
                "GuestName", "GuestPhone", "Reason",
                "MaxUses", "UsedCount",
                "ValidFrom", "ValidTo",
                "DoorPermissions", "CreatedAt", "IsUploadedToDevice"
            )
            VALUES (
                (start_from + i)::text,
                0,                                -- Available
                'Cloud',
                '', '', '',
                2, 0,
                NOW(),
                NOW() + INTERVAL '5 years',
                '01010101', NOW(), false
            )
            ON CONFLICT ("Code") DO NOTHING;
        END LOOP;
        RAISE NOTICE 'Seed complete.';
    END IF;
END $$;

\echo ''
\echo '=== Step 3: final count by Source ==='
SELECT "Source", "Status", COUNT(*) AS count
FROM "QrPool"
GROUP BY "Source", "Status"
ORDER BY "Source", "Status";
