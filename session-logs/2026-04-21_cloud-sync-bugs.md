# Session Log — 2026-04-21

**Session start:** 2026-04-21 (afternoon)
**Session end:** 2026-04-21 (evening)
**Branch:** feature/pwa-cloud
**Goal:** Set up log file rotation + investigate customer log files for bugs

---

## Part 1 — Log file rotation setup

### What we built
Added a shared helper that wraps every diagnostic log file in the app with:

1. **Date + time on every line** (`[2026-04-21 14:30:00.123]`)
2. **Separator banner** on each app launch (`#############` line with `# APP LAUNCH — timestamp`)
3. **Separator banner** when date rolls over (`# NEW DAY — 2026-04-22`)
4. **60-day rolling trim** — DVR-style: keep recent 60 days, drop oldest, never wipe-everything
5. **Legacy archive** — old log files without dates on each line get renamed to `{name}_legacy_{date}.txt` on first launch, so history isn't lost
6. **Streaming trim** — safe for huge files (100+ MB), doesn't load into RAM

### Files touched (log rotation)
- NEW: `src/AccessControlPro.Application/Services/RollingLogFile.cs` — main helper
- NEW: `src/AccessControlPro.SDK/Wrapper/SdkLogFile.cs` — duplicate for SDK project (standalone, no reference to Application)
- MODIFIED: `src/AccessControlPro.Application/Services/BackupService.cs`
- MODIFIED: `src/AccessControlPro.Application/Services/EmployeeService.cs` (card disable log)
- MODIFIED: `src/AccessControlPro.Application/Services/CloudSyncService.cs`
- MODIFIED: `src/AccessControlPro.Application/Helpers/CardSyncRunner.cs`
- MODIFIED: `src/AccessControlPro.WPF/App.xaml.cs` (startup log)
- MODIFIED: `src/AccessControlPro.Admin/App.xaml.cs` (admin startup log)
- MODIFIED: `src/AccessControlPro.SDK/Wrapper/AccessControlSdkWrapper.cs` (sdk log)

### 7 log files now use the helper
- backup_log.txt
- card_disable_log.txt
- cloud_sync_log.txt
- startup_log.txt
- admin_startup_log.txt
- sdk_log.txt (two writers share the same file)

---

## Part 2 — Customer log file investigation

### Customer files analyzed
Located in `d:\AccessControlPro\basmia\`:
- backup_log.txt (2.5 KB) — clean, no issues
- card_disable_log.txt (500 B) — clean
- **cloud_sync_log.txt (119 MB)** — huge, full of errors
- **startup_log.txt (118 MB)** — huge, duplicate of cloud_sync content
- sdk_log.txt (2.1 MB) — clean, no device errors

### Bugs discovered (8 total)

#### FIXED THIS SESSION

**Bug 1 — FreezeHistories schema mismatch**
Column `FreezeStart` in cloud PostgreSQL is `TIMESTAMP` but the server's JSON deserializer was sending it as `text` because the column name wasn't in the "date column" list.
- File: `src/AccessControlPro.Web/Data/SyncHelper.cs:179`
- Fix: added `FreezeStart` and `FreezeEnd` to `IsDateColumn()`
- Impact: eliminates ~2218 errors per sync

**Bug 2 — Players.Height/Weight numeric overflow**
Customer's player row 786 has an invalid Height or Weight value (probably someone typed a phone number into the Height field). Column was `DECIMAL(5,1)` = max 999.9.
- File: `src/AccessControlPro.Web/Data/DbHelper.cs:43-44`
- Fix: widened to `DECIMAL(10,2)` + added `ALTER TABLE` migration so existing cloud DBs upgrade automatically
- Impact: no more 22003 overflow errors

**Bug 3 — Single-transaction cascade (biggest bug)**
When row 786 failed, PostgreSQL aborted the whole transaction. Every subsequent row got "transaction aborted" error — 2244 out of 3029 good rows were being dropped every sync because of one bad record.
- File: `src/AccessControlPro.Web/Data/SyncHelper.cs:62-130`
- Fix: wrapped each row in a `SAVEPOINT` / `ROLLBACK TO SAVEPOINT` so one bad row no longer poisons the transaction
- Impact: the 74% data loss per sync is gone

**Bug 6 — Log file bloat (118 MB files)**
Every 5 minutes the full JSON response (including error arrays with thousands of entries) was being dumped into logs. Response lines were reaching 200+ KB each.
- File: `src/AccessControlPro.Application/Services/CloudSyncService.cs`
- Fix: added `SummarizeSyncResponse()` + `LogResponseSnippet()` — logs only summary + first 3 errors
- Impact: log files will stay small going forward

**Bug 7 — Duplicate logging to two files**
Cloud sync result (including the huge JSON) was being logged to both `cloud_sync_log.txt` AND `startup_log.txt` because `SyncToCloudAsync()` returned the raw response and `App.xaml.cs` re-logged it.
- File: `src/AccessControlPro.Application/Services/CloudSyncService.cs`
- Fix: sync method now returns a short summary string like `"Synced: ok=true, total=10764, errors=3"`
- Impact: startup_log will no longer receive the huge sync dumps

**Bug 4 (partial) — Full sync instead of delta**
Current sync sends ALL rows every 5 minutes, even unchanged ones. 4 MB uploaded per sync.
- File: `src/AccessControlPro.Web/Data/SyncHelper.cs`
- Partial fix: added `ON CONFLICT (Id) DO UPDATE SET ...` UPSERT clause (foundation for delta sync)
- Still pending: client-side filtering by `UpdatedAt` — see "Outstanding work" below

#### DIAGNOSTIC ADDED (NOT YET ROOT-CAUSED)

**Bug 5 — App restart loop**
Customer's app restarted 113 times in 5 days (~22 per day). No crash stack traces in the logs — so either customer didn't copy `crash_log.txt`, or the app isn't crashing (being force-killed externally, or user is launching it multiple times).
- File: `src/AccessControlPro.WPF/App.xaml.cs:847-849`
- Diagnostic added: `=== APP EXITING (code=N) ===` log line on graceful shutdown
- **NEXT STEP:** deploy new build, run on customer PC for a few days, pull new `startup_log.txt`. Read the sequence of STARTING and EXITING lines. If every STARTING has a matching EXITING before it → clean exits (normal user behavior). If not → something is killing the process externally.

#### ENVIRONMENTAL (NOT A CODE BUG)

**Bug 8 — Intermittent VPS timeouts**
Multiple `hmtech.solutions:443` connection timeouts and DNS failures in the log. Already handled as "non-critical" in code. Either VPS was intermittently down or customer's internet dropped. No code change.

---

## Part 3 — Outstanding work (for next session)

### Priority 1 — Deploy what we fixed today
Ship new WPF build + redeploy `AccessControlPro.Web` backend to VPS. Migrations run automatically (Height/Weight column widens on PG startup).

### Priority 2 — Wait for new customer logs (3-5 days)
- Read `startup_log.txt` for `=== APP EXITING ===` pattern
- If kills are happening → dig deeper (Windows Event Viewer, antivirus, power management)
- If clean exits → problem is user behavior, not code (may not need a fix)

### Priority 3 — Height/Weight input validation
**Why:** today's fix accepts the bad data. It should reject it upstream.
**What:** In the WPF player form (probably `EmployeeFormView` or similar), add validation:
- Height: must be between 50 cm and 250 cm
- Weight: must be between 20 kg and 300 kg
- Reject input outside range with a clear error message
**Size:** ~1 hour

### Priority 4 — True delta sync (Bug 4 full fix)
**Why:** bandwidth optimization — stop sending all 10,000 rows every 5 minutes
**What:**
1. Add `UpdatedAt TIMESTAMP` column to every table (both SQL Server and PostgreSQL)
2. Set `UpdatedAt = GETUTCDATE()` wherever rows are modified
3. Client tracks `lastSyncAt` (save to appsettings.json or a state file)
4. Change `ReadTableAsync()` SQL to add `WHERE UpdatedAt > @lastSyncAt`
5. On server side, change the DELETE-then-INSERT to pure UPSERT (the `ON CONFLICT` clause is already in place from today's work)
6. Handle deletes via the existing `DeletedEmployees` tombstone pattern
**Size:** 1-2 days of careful work across ~30 files

---

## Git state at end of session
- Branch: `feature/pwa-cloud`
- Changes: **not committed**. Many `M` (modified) files. The cloud sync bug fixes are local only.
- Recommended commit message if you commit tomorrow:

```
Fix cloud sync: savepoints, FreezeHistories timestamp, Height/Weight overflow

- SyncHelper: per-row SAVEPOINT isolates failures (no more transaction cascade — was dropping 74% of rows per sync)
- SyncHelper: ON CONFLICT upsert (foundation for delta sync)
- SyncHelper: add FreezeStart/FreezeEnd to date column list (eliminates 2218 errors per sync)
- DbHelper: widen Players.Height/Weight to DECIMAL(10,2) + migration
- CloudSyncService: truncate sync response log (was writing 200+ KB per line, 118 MB total files)
- CloudSyncService: return short summary instead of full JSON
- App.xaml.cs: log OnExit so we can detect force-kills on customer machines
- RollingLogFile / SdkLogFile helpers: 60-day rolling retention + launch/day separators + legacy archive
```

---

## Files to read first tomorrow

If you or a future Claude session wants to pick up the work:

1. **This file** — full context
2. `d:\AccessControlPro\basmia\startup_log.txt` — **new** customer log (once collected) — look for `=== APP EXITING ===` presence
3. `src/AccessControlPro.Web/Data/SyncHelper.cs` — all server-side sync logic
4. `src/AccessControlPro.Application/Services/CloudSyncService.cs` — all client-side sync logic

---

## Notes

- Build verified clean: 0 errors across entire solution (44 pre-existing warnings, none from today's changes)
- Memory entry saved at `~/.claude/projects/d--AccessControlPro/memory/rolling_log_file.md` so future Claude sessions follow the same logging convention when adding new log sinks
- No changes were committed or pushed — all work is local on `feature/pwa-cloud` branch

End of session.
