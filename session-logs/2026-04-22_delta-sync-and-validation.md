# Session Log — 2026-04-22

**Session start:** 2026-04-22
**Branch:** feature/pwa-cloud
**Goal:** Priority 3 (Height/Weight validation) + Priority 4 (delta sync)

Previous session: [2026-04-21_cloud-sync-bugs.md](2026-04-21_cloud-sync-bugs.md)

---

## Priority 3 — Height/Weight input validation ✅

Added client-side validation in the player form so bad numbers never reach the database.

### Behavior
- Height field: empty is allowed (optional), else must be **50-250 cm**
- Weight field: empty is allowed (optional), else must be **20-300 kg**
- Validation error shows bilingual message in the same dialog as other field errors

### Files changed
- `src/AccessControlPro.WPF/Resources/Strings.resx` — added `HeightInvalid` / `WeightInvalid` (English)
- `src/AccessControlPro.WPF/Resources/Strings.ar.resx` — added Arabic translations
- `src/AccessControlPro.WPF/Helpers/LanguageManager.cs` — exposed as `Lang.HeightInvalid` / `Lang.WeightInvalid` properties
- `src/AccessControlPro.WPF/Views/AddEmployeeDialog.xaml.cs` — range checks in `OkButton_Click`

### Impact
- Next time a user types a phone number into the Height field by mistake, they'll see a clear error before the record is saved.
- Combined with yesterday's server-side column widening (DECIMAL(5,1) → DECIMAL(10,2)), this is defense in depth: UI rejects typos, and even if something slips through, the cloud column accepts it without overflow.

---

## Priority 4 — True delta sync ✅

Client now sends only **rows changed since last successful sync** instead of all rows every 5 minutes.

### How it works

**Client side:**
1. On startup/sync-time, read `sync_state.json` → get `lastSyncAt` (null on first-ever sync)
2. Capture `syncStartedAt = UtcNow` **before** reading any data
3. For each table, build the SELECT query:
   - Mutable tables (Players, Users, AccessCards, etc.): `WHERE UpdatedAt > @since`
   - Append-only tables (AccessEvents, AuditLogs, Transactions, DeletedEmployees): `WHERE [Timestamp/CreatedAt/DeletedAt] > @since`
   - Small static tables (Devices, Doors, AppSettings, TimeGroups): always full sync (few rows, cheap)
   - Full sync path (first time): omit WHERE clauses entirely
4. Send HTTP header `X-Sync-Mode: full` or `X-Sync-Mode: delta`
5. **Only on success**, save `syncStartedAt` as new `lastSyncAt`

**Server side:**
1. Read `X-Sync-Mode` header (default = `full` for backward compat with old clients)
2. For each table:
   - Full mode: `DELETE FROM table; INSERT ...` (existing behavior)
   - Delta mode: skip the DELETE, just UPSERT changed rows
3. Special case for deletes: when processing `DeletedEmployees` tombstones in delta mode, call `SyncHelper.ApplyDeleteTombstonesAsync()` which issues `DELETE FROM Players WHERE Id = OriginalId` for each tombstone. That way player deletions still propagate to the cloud.

### Schema changes (local SQL Server)

New migration `v4.5` in [DatabaseMigrator.cs](../src/AccessControlPro.Infrastructure/Persistence/DatabaseMigrator.cs):

Columns added:
- Employees.UpdatedAt
- AccessCards.UpdatedAt
- QrPool.UpdatedAt
- Users.UpdatedAt
- FreezeHistories.UpdatedAt
- Products.UpdatedAt
- SubscriptionPlans.UpdatedAt
- PosShifts.UpdatedAt

All default to `GETUTCDATE()` on INSERT. All have an AFTER UPDATE trigger (`TR_<Table>_UpdatedAt`) that sets `UpdatedAt = GETUTCDATE()` automatically on any UPDATE. **No repository code needed to change** — triggers maintain it transparently.

Index added: `IX_Employees_UpdatedAt` (hot path for delta queries on the biggest table).

### Safety properties

- **No data loss on failure:** if sync fails or crashes mid-way, `lastSyncAt` doesn't advance. Next attempt re-sends everything since the last successful watermark.
- **Race safety:** watermark is captured BEFORE reading data. A row updated at the exact moment of sync re-sends once next round (harmless — upsert is idempotent).
- **First-ever sync:** `lastSyncAt` is null → client sends `X-Sync-Mode: full` → server wipes tables and loads everything (existing behavior).
- **Recovery:** delete `sync_state.json` → next sync becomes full sync automatically.

### Files changed
- NEW: `src/AccessControlPro.Application/Services/SyncStateManager.cs` — reads/writes `sync_state.json`
- `src/AccessControlPro.Infrastructure/Persistence/DatabaseMigrator.cs` — v4.5 migration
- `src/AccessControlPro.Application/Services/CloudSyncService.cs` — delta queries, mode header, watermark save on success
- `src/AccessControlPro.Web/Program.cs` — read X-Sync-Mode header, pass to SyncHelper
- `src/AccessControlPro.Web/Data/SyncHelper.cs` — `isFullSync` parameter + `ApplyDeleteTombstonesAsync` method

### Expected impact on customer
- **Before:** every 5 minutes = 10,764 rows / 4 MB JSON / ~300 KB compressed
- **After (typical day):** every 5 minutes = ~0-20 changed rows / ~5 KB JSON / ~2 KB compressed
- **Bandwidth savings:** roughly 99%
- **Server load:** massively reduced (no more DELETE-then-INSERT-10k-rows every 5 min)

---

## Build status

Full solution build: **0 errors, 44 warnings (all pre-existing)**.

---

## Outstanding work

### Priority 1 (waiting on data) — Root-cause the restart loop
Still need fresh customer logs (3-5 days after deploying yesterday's `=== APP EXITING ===` diagnostic). When you get them:
- Read `startup_log.txt`
- Check if every `APP STARTING` has a preceding `APP EXITING` (clean exit)
- If not → something is force-killing the process (Windows, antivirus, user)

### Priority 5 (nice-to-have) — Delta sync monitoring
Would be useful to add a "Force full sync" button in the admin UI for recovery scenarios. Currently only way is to delete `sync_state.json` on disk. ~30 min task.

---

## Git status at end of session

Many uncommitted files including new session-logs folder. Committing and pushing as part of this session per user request.

---

## Suggested commit message

```
Add Height/Weight validation + delta cloud sync

- AddEmployeeDialog: reject Height outside 50-250 cm and Weight outside 20-300 kg
- Strings.resx + Strings.ar.resx: bilingual HeightInvalid / WeightInvalid messages
- DatabaseMigrator v4.5: UpdatedAt column + AFTER UPDATE trigger on all mutable tables
- SyncStateManager: persist lastSyncAt in sync_state.json so next sync can skip unchanged rows
- CloudSyncService: delta queries for all mutable / append-only tables, X-Sync-Mode header, watermark-on-success
- SyncHelper: isFullSync flag skips DELETE in delta mode; ApplyDeleteTombstonesAsync propagates local deletes via DeletedEmployees
- Program.cs: read X-Sync-Mode header from client

Bandwidth drops ~99% in delta mode (typical: ~20 changed rows every 5min vs 10,764 before).
First-ever sync still runs as full sync automatically (null lastSyncAt).
Safe on failure: watermark only advances on success, so nothing is ever missed.
```

End of session.
