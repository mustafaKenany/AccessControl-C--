-- =============================================================================
-- Backfill Basmia gym's subdomain
-- =============================================================================
-- Date    : 2026-05-14
-- Run on  : VPS master DB (gymcloud), AFTER the new web build is deployed.
--
-- Once the GymSubdomainMiddleware is live, basmia.hmtech.solutions/login will
-- start working as soon as this row has Subdomain = 'basmia'. Until then,
-- hmtech.solutions/login still works as the fallback (soft migration).
-- =============================================================================

\c gymcloud

-- Show the current row before update
\echo 'Current gym record:'
SELECT "Id", "Name", "Subdomain", "DatabaseName", "ApiKey", "IsActive"
FROM "Gyms"
WHERE "DatabaseName" = 'gymcloud_drag';

-- Backfill subdomain
UPDATE "Gyms"
SET "Subdomain" = 'basmia'
WHERE "DatabaseName" = 'gymcloud_drag'
  AND ("Subdomain" IS NULL OR "Subdomain" = '');

-- Confirm after update
\echo ''
\echo 'After update:'
SELECT "Id", "Name", "Subdomain", "DatabaseName", "IsActive"
FROM "Gyms"
WHERE "DatabaseName" = 'gymcloud_drag';
