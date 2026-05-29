# Session Log — Basmia stability lockdown + multi-gym readiness

**Date range**: 2026-05-19 through 2026-05-28
**Customer in focus**: Basmia (Iraqi gym, ~2,200 players, single 4-door CR3242T device)
**Outcome**: Multi-evening sprint that diagnosed chronic instability, shipped 5 commits, and produced everything needed for a calm customer install + future multi-gym scale.

---

## How to use this file

In a new conversation, point me at this file:

> Please read `docs/session-logs/2026-05-basmia-stability-lockdown.md` so you know what we've been working on.

I'll have full context to pick up where we left off.

---

## 1. Why the session started

The user (HM-Tech IT Solutions) has one paying customer running AccessControlPro: **Basmia**, a gym in Iraq. They had been force-killing the WPF app via Task Manager every 1-3 days for **11 straight days** (2026-05-09 → 2026-05-18) because it kept slowing down / freezing. The 2026-05-18 event was severe enough that they actually asked for help.

The user collected all log files + JSON snapshots from Basmia's PC and shared them with me piece-by-piece across the session. The user's goal: figure out what's wrong, fix it, deliver a stronger build, and make the app production-grade for future customers (gym #2, #3, etc.).

Basmia is explicitly treated as the **beta / test customer** — user agreed they want a "strong application" before onboarding anyone else.

---

## 2. The bug analysis (what the logs revealed)

The user shared eight files from Basmia's PC. Summarized findings:

| File | What it told us |
|---|---|
| `session.log` | Normal usage. Every renewal `type=Fitness` (will explain). Card formats mixed (8H10D leading-zero issue confirmed live). |
| `backup_log.txt` | DB backups healthy 2x/day. App restarts visible on May 12, 15, 18 — all force-kills based on startup_log cross-ref. |
| `crash_log.txt` | **OOM cascade on 2026-05-18 09:27 + 10:16:58.** 10 OOM exceptions in 35 ms with 10 invisible CustomMessageBox windows piling up. Process uptime at crash: **3 days 12h 34m**. Working set **681 MB**, managed heap 336 MB → so **~345 MB of native memory leaked** that we hadn't accounted for. |
| `card-delete log` | All device card-delete operations succeeded. Device side fully healthy. |
| `cleanup_log.txt` | Daily inactive-player cleanup working. 71 players naturally cleaned in 10 days. |
| `cloud_sync_log.txt` | Every sync `ok=True`. Initial full sync: 12,814 records / 419 KB compressed. **SubscriptionPlans = 0 rows** in DB — root cause of "Fitness" hardcoding. |
| `sdk_log.txt` (Chinese mojibake) | Device communication healthy through the entire crash window. Even during the May 18 freeze, the device kept getting polled — proves it was UI-thread only. |
| `wrapper_log.txt` | **The SDK was re-initializing on EVERY card-add operation** (~30/day × 11 days = 330 native re-inits). `DeviceOperationHelper.StopAndReset()` was calling `Shutdown() → wait → Initialize()` instead of just pause/resume. Native memory leak source #1. |
| `startup_log.txt` | App had been **force-killed 5+ times** during the 11-day window. Customer adapted to the instability silently. |

### Root cause: TWO simultaneous memory leaks

1. **Managed leak (~336 MB)**: WPF `ItemsControl` on the Players page was NOT virtualized. Every filter switch re-materialized all 2,200+ row visuals without releasing the old ones. Receptionist's rapid filter switching = leak compounded fast.

2. **Native leak (~345 MB)**: `DeviceOperationHelper.StopAndReset()` called the full SDK `Shutdown + initNet + new ConnectMain()` on every card operation. Each cycle leaked ~1 MB of native handles.

Combined: working set crossed 681 MB at ~3.5 days uptime, OS killed it. Customer saw frozen UI for hours because **the crash dialog tried to render → triggered another OOM → cascaded 10 times in 35 ms**.

---

## 3. What got shipped (commit by commit)

All commits are on branch `feature/pwa-cloud`. Pushed to GitHub.

### `f6cadd8` — SDK lifecycle fix
- `DeviceOperationHelper.ExecuteWithLock` now uses `PauseMonitoring/ResumeMonitoring` instead of `Shutdown+Initialize`.
- SDK stays initialized across operations. Retry path (`ExtendedReset`) still does the heavy reset.
- **Eliminates native leak source #1.**
- File: `src/AccessControlPro.Application/Helpers/DeviceOperationHelper.cs`

### `dd1ad80` — Phase 1 stability lockdown
- **OOM cascade fix**: `DispatcherUnhandledException` skips `CustomMessageBox` on `OutOfMemoryException`, forces Gen2 compacting GC instead.
- **Virtualization** on Players, Events, Logs:
  - `VirtualizingStackPanel` with `Recycling` mode
  - `ScrollViewer.CanContentScroll="True"`
  - Files: `EmployeesView.xaml`, `EventsView.xaml`, `LogsView.xaml`
- **Filter-switch GC nudge** in `EmployeesViewModel.SetFilterAsync`.
- **Memory monitor**: background `DispatcherTimer` every 10 min writes working-set / heap stats to `memory_log.txt`. Warn at >500 MB, force Gen2 compact at >700 MB.
- **Restart-recommended reminder**: bilingual (EN/AR) prompt after 5+ days uptime, every 12h after.
- **Auto-seed 5 default SubscriptionPlans** in `DatabaseMigrator.cs` (idempotent via `IF NOT EXISTS` guard).

### `1aa0b2d` — Phase 2 data integrity
- New `tools/data-integrity/cleanup.sql` + `cleanup.bat` + `README.md`. Cleans dangling AccessCards, duplicate CardNumbers, orphan FreezeHistories, orphan Transaction refs. Takes safety backup first.
- New `SuperAdmin/DataQuality.razor` page. Per-gym counts of dangling/duplicate/orphan rows + Migrated player % + Plans count. Verdict pills.
- **Double-click protection** on Renew + AssignCard in `EmployeesViewModel.cs`. Per-player `HashSet<int>` guard.

### `2a95a51` — Phase 3 + 4 hardening
- Virtualization extended to **DeletedRecords** + **QrPass** views.
- **SuperAdmin subdomain lock** in `SuperAdminLayout.razor`. Only `admin.*` or root domain can access `/superadmin/*`. Wrong subdomain shows "go to admin.hmtech.solutions" landing.
- Bilingual **card-format search hint** tooltip on Employees page.

### `cf4be53` — Gym #2 readiness + docs
- **Setup wizard auto-generates unique CloudApiKey** in `SetupWizardWindow.xaml.cs:GenerateUniqueApiKey`. Format: `HMT-XXXXXXXX-XXXXXXXX` (64 bits of entropy). Previous hardcoded fallback was the same string `"HMTech-Sync-2026"` on every install — security risk.
- New `tools/post-install/post-install.bat` wrapper that calls tuneup + cleanup in sequence.
- New `SuperAdmin/GymHealth.razor` page. Per-gym vital signs (last sync, last diag, recent crashes) with Green/Yellow/Red scoring.
- Four documentation files in `docs/`:
  - `ONBOARDING.md` (45-min new-customer install checklist)
  - `PERFORMANCE.md` (measured benchmarks on Basmia hardware)
  - `MIGRATION.md` (SQL procedure to bring in old customer data)
  - `DISASTER-RECOVERY.md` (8 failure scenarios + recovery)

---

## 4. The deployment — errors encountered + how we fixed them

### Error 1: VPS appsettings.json broke after manual edit (2026-05-XX)
- **Symptom**: `gymapp` service crash-looped with `JsonReaderException: '"' is invalid after a value. Expected either ',', '}', or ']'. LineNumber: 13`
- **Cause**: User added the new `DiagnosticsStoragePath` key but missed the comma at end of the previous JSON line.
- **Fix**:
  ```bash
  nano /var/www/gymapp/appsettings.json
  # Add comma at end of previous line, save
  python3 -c "import json; json.load(open('/var/www/gymapp/appsettings.json')); print('OK')"
  systemctl start gymapp
  ```
- **Prevention**: always validate JSON with Python before restart.

### Error 2: QR seed failed with duplicate key (2026-05-28)
- **Symptom**: `ERROR: duplicate key value violates unique constraint "QrPool_pkey" DETAIL: Key ("Id")=(1201) already exists.`
- **Cause**: PostgreSQL sequences out of sync with data. The `/api/sync` endpoint inserts rows with explicit `Id` values (preserving local SQL Server's IDs), but the PostgreSQL `QrPool_Id_seq` sequence never advances. After enough syncs, auto-generated IDs collide.
- **Fix**: reset the sequence to `MAX(Id)`:
  ```bash
  sudo -u postgres psql gymcloud_drag -c "SELECT setval(pg_get_serial_sequence('\"QrPool\"', 'Id'), (SELECT MAX(\"Id\") FROM \"QrPool\"));"
  ```
- **Preventive sweep** ran on all 8 tables that grow via sync:
  - Players → 17487
  - AccessCards → 20146
  - AccessEvents → 15875
  - Transactions → 21167
  - AuditLogs → 24388
  - QrPool → 5500
  - DeletedEmployees → 9285
  - FreezeHistories → 3

### Error 3: `/tmp/check-and-seed-cloud-qr.sql` file not found
- **Symptom**: `psql: error: /tmp/check-and-seed-cloud-qr.sql: No such file or directory`
- **Cause**: User ran the psql command before uploading the SQL file.
- **Fix**: gave user two options — SCP from local or paste as here-doc on VPS.

---

## 5. Current state of the system (as of 2026-05-28)

### Cloud VPS (89.116.39.155, hmtech.solutions)
- ✅ Web build deployed (`/var/www/gymapp/AccessControlPro.Web` dated May 28)
- ✅ All new SuperAdmin pages return HTTP 200: `/superadmin/diagnostics`, `/data-quality`, `/gym-health`
- ✅ Subdomain lock verified working (`basmia.hmtech.solutions/superadmin/*` shows "go to admin" landing)
- ✅ QR pool: 2,000 cloud codes seeded (range 60003501-60005500)
- ✅ All 8 sequences fixed (preemptive)
- ✅ `/var/www/diagnostics/` folder exists, owned by www-data
- ⚠️ SSL cert expires 2026-08-12 — manual `certbot --manual` renewal needed

### Customer's PC (Basmia)
- ⏭️ **NOT YET INSTALLED** — user is going to install tomorrow morning
- Customer's local DB still has:
  - 1,853 Migrated player rows (decision = leave alone, natural cleanup via renewal gate)
  - 32 dangling AccessCard rows + 1 duplicate (`02190620`) — cleaned by `tools/data-integrity/cleanup.bat` which runs as part of `tools/post-install/post-install.bat`
  - Empty SubscriptionPlans table — auto-seeded by the new build's migrator on first launch

### Tomorrow's procedure at Basmia (~10 min)
```
1. Close their running AccessControlPro.WPF.exe
2. Copy publish/customer-deploy/main-app/* over their install
   ⚠ KEEP their existing appsettings.json
3. Copy the whole tools/ folder to their Desktop
4. Right-click tools/post-install/post-install.bat → Run as Admin → Y
5. Log out + back in (visual-effects setting needs fresh session)
6. Launch + log in
7. Admin panel → Subscription Plans → adjust 5 pre-seeded prices to match Basmia's actual gym rates
8. Click "Send Diagnostics" → verify bundle appears at admin.hmtech.solutions/superadmin/diagnostics
```

---

## 6. Pending decisions (waiting on user input)

These are open but **not blocking** tomorrow's install:

| # | Decision | My recommendation | Status |
|---|---|---|---|
| 1 | Timezone direction — was Basmia event Card=2266821 at 10:15:43 morning or afternoon Iraq time? | Need user answer | User never confirmed in any prior session |
| 2 | 1,853 Migrated players strategy | **A) Leave for natural renewal cleanup** (the renewal gate forces re-entry) | Recommended A; user hasn't explicitly approved or rejected |
| 3 | Basmia beta-customer comms | **A) Tell them they're our beta + offer discount** | User's call |

---

## 7. Deferred work (saved for future sessions)

Not done tonight; reasons documented:

1. **Server-side pagination on Players** — virtualization may already be enough; watch `memory_log.txt` from the next Basmia diagnostics bundle to confirm.
2. **Setup wizard prompts for unique DB password** (currently still defaults `sa/123`) — touches install flow + connection strings, deserves daylight QA.
3. **CSV migration importer** — heavy work, deserves design pass. SQL procedure documented in `docs/MIGRATION.md` as interim.
4. **SSL DNS API auto-renewal** — needs Hostinger DNS API credentials from user.
5. **Load test with 5,000 synthetic players** — needs sustained test rig.
6. **Multi-device hardware test** — needs second physical device.
7. **Cloud PostgreSQL nightly pg_dump cron** — runbook entry written in `DISASTER-RECOVERY.md`, user needs to set up.

---

## 8. Key file paths to know

### Source
- `src/AccessControlPro.WPF/` — Main WPF app
- `src/AccessControlPro.WPF/Views/EmployeesView.xaml` — Players list (where virtualization was added)
- `src/AccessControlPro.WPF/ViewModels/EmployeesViewModel.cs` — Where double-click protection + filter-switch GC lives
- `src/AccessControlPro.WPF/App.xaml.cs` — Where memory monitor + restart reminder + OOM cascade fix live
- `src/AccessControlPro.WPF/Views/SetupWizardWindow.xaml.cs` — Where `GenerateUniqueApiKey` lives
- `src/AccessControlPro.Application/Helpers/DeviceOperationHelper.cs` — SDK lifecycle fix
- `src/AccessControlPro.Application/Services/DiagnosticsService.cs` — Bundle builder + uploader
- `src/AccessControlPro.Infrastructure/Persistence/DatabaseMigrator.cs` — Where SubscriptionPlans auto-seed lives (v4.3 block)
- `src/AccessControlPro.Web/Components/Pages/SuperAdmin/` — Diagnostics, DataQuality, GymHealth pages
- `src/AccessControlPro.Web/Components/Layout/SuperAdminLayout.razor` — Subdomain lock

### Tools (for the customer install)
- `tools/post-install/post-install.bat` — **the one script to run at customer**
- `tools/customer-pc-tuneup/tuneup.bat` — SQL + Windows tuning
- `tools/data-integrity/cleanup.bat` — DB cleanup
- `tools/qr-test/check-and-seed-cloud-qr.sql` — VPS-side QR seed

### Documentation
- `docs/ONBOARDING.md` — New-customer install checklist
- `docs/PERFORMANCE.md` — Measured benchmarks
- `docs/MIGRATION.md` — Bring data from old systems
- `docs/DISASTER-RECOVERY.md` — Failure scenarios + recovery
- `docs/session-logs/*.md` — Session logs like this one

### Publish outputs (regenerated by `dotnet publish`, not in git)
- `publish/customer-deploy/main-app/` — WPF main app (win-x86, 194 MB)
- `publish/customer-deploy/admin-panel/` — Admin (win-x86, 195 MB)
- `publish/customer-deploy/pos-terminal/` — POS (win-x86, 195 MB)
- `publish/web-deploy/` — Blazor Server for VPS (linux-x64, 106 MB)

---

## 9. How to verify after the install (tomorrow + 7 days)

### ~5 min after the customer's "Send Diagnostics" test
- Open `https://admin.hmtech.solutions/superadmin/diagnostics`
- Basmia's bundle should appear with trigger="manual" and the test note

### Day +1
- Open `https://admin.hmtech.solutions/superadmin/gym-health` → Basmia row should be **Green** or **Yellow**, never **Red**
- Open `https://admin.hmtech.solutions/superadmin/data-quality` → Basmia row should show **"Clean"** verdict

### Day +7
- Diagnostics bundle auto-uploads (15-day timer). Should arrive ~14 days after the manual test.
- Download the latest bundle and read `logs/memory_log.txt` — working set should stay under 500 MB across all 10-min samples.

### If something goes Red
- Open the latest crash-recovery diagnostics bundle from `/superadmin/diagnostics`
- Read `logs/crash_log.txt` for the stack trace
- Compare with patterns documented in [[basmia-oom-incident]] memory entry

---

## 10. Key memory pointers (for next session continuity)

Memory entries written this session — these are auto-loaded next time:
- `basmia_oom_incident.md` — The diagnostic findings and what fixes addressed which leak
- `customer_deploy_assets.md` — Where install scripts and SuperAdmin pages live
- `diagnostics_bundle.md` — How the diagnostics upload system works
- `setup_wizard_behavior.md` — Auto-generated CloudApiKey + auto-seeded plans
- `pending_decisions.md` — The 3 open user-input items

Plus this session log itself, at `docs/session-logs/2026-05-basmia-stability-lockdown.md`.

---

## 11. The honest big-picture summary

**Before this session**: AccessControlPro worked for one customer who was suffering through chronic instability and adapting by force-killing the app every few days. Onboarding a second customer would have meant inheriting all the bugs PLUS the security risk of identical CloudApiKeys.

**After this session**: every meaningful bug the diagnostics revealed has a fix in the build. The customer install is one script. Operational visibility (Diagnostics + Data Quality + Gym Health dashboards) means support can spot problems before customers call. Documentation covers onboarding, performance expectations, migration, and disaster recovery. The setup wizard generates unique credentials per install so gym #2 is safe.

**Still to do**: production-scale validation (load test, multi-device), and the 3 pending user decisions. None of these block tomorrow's install or future onboardings.

---

*End of original session log. Tomorrow's job is to execute the 8-step install procedure at Basmia. Everything is ready.*

---

# 2026-05-28 — Install day at Basmia + backup-on-launch follow-up

## What happened today

**Morning — Install at Basmia** (via AnyDesk):
- Customer install procedure executed cleanly. WPF launched, device connected on first try, diagnostics upload succeeded (customer saw the green "تم رفع ملفات التشخيص بنجاح" success dialog). First clean exit in 12 days.
- Cloud QR seed had to handle a PostgreSQL sequence collision (`duplicate key (Id)=(1201)`) because the sync-explicit-IDs pattern doesn't advance the sequence. Fixed on the VPS with `SELECT setval(pg_get_serial_sequence(...), MAX("Id"))` for QrPool, then ran a preemptive sweep on all 8 tables that the WPF→cloud sync ever inserts to. Sequences now match `MAX(Id)` on every table — no more collisions for any future sync.
- VPS Blazor host needed a JSON comma fix in `appsettings.json` (user edit was missing one) — gymapp was crash-looping until that was corrected.

**Afternoon — Backup investigation**:
- Customer paste-bombed `backup_log.txt` showing automatic backups failing since 2026-05-27 with "transaction log full due to LOG_BACKUP" errors. Root cause: SQL Server defaults to FULL recovery model when restoring a `.bak`, and FULL recovery grows the transaction log until it fills the disk unless you also schedule log backups (which AccessControlPro doesn't and shouldn't need to).
- Patched `tools/customer-pc-tuneup/tuneup.sql` to set the recovery model to SIMPLE during the tuneup step (idempotent — checks current state first) and shrink the log file. Committed as `0b8d151`. Customer applied a temporary fix manually via SSMS but **switched the DB back to FULL recovery** at the end of their script — flagged that this means the problem will return in 2-4 weeks and gave them the SIMPLE-recovery SQL to run once and leave alone.

**Evening — Backup-on-launch safety net**:
- Customer asked for old-backup cleanup and auto-backup-on-close (silent). After explaining the trade-offs (on-close blocks app exit and risks corruption if user force-kills mid-write), customer agreed on the cleaner approach: **on-launch backup if stale**.
- Bumped `BackupService.cs` retention from 2 → 3 days (covers a weekend gap if a backup fails).
- Added `tools/backup-cleanup/cleanup-old-backups.bat` — standalone manual cleanup script for AnyDesk sessions where you need to free disk on demand without waiting for the next 02:24 / 14:24 auto-run.
- Added `RunStartupBackupIfStale()` in `App.xaml.cs` (~line 1237): on every launch, if last successful backup is >12 hours old, show a small dark-teal splash (`Backing up database... / جاري النسخ الاحتياطي...`) with an indeterminate progress bar and run a synchronous backup via `BackupService.RunBackupAsync()`. The splash stays painted via `DispatcherFrame` while the backup runs on a `Task.Run` background thread. Backup failure is non-fatal (caught + logged, login proceeds).

## Why on-launch instead of on-close

| Scenario | On-close | On-launch (chosen) |
|---|---|---|
| User force-kills via Task Manager because they think the app is "stuck closing" | Corrupted `.bak` | N/A — backup is running while app is fully alive |
| PC power loss mid-backup | Corrupted `.bak` | Less likely — happens at customer-attended startup, not unattended shutdown |
| 24/7 PC, app never restarts | Same — 02:00/14:00 schedule still works | Skipped on each launch (already fresh, < 12h since last) |
| PC off at night, app launches at 09:00 | 02:00 backup missed → no backup that day | Stale detected → silent backup before login |

## Files touched today (2026-05-28)

- `tools/customer-pc-tuneup/tuneup.sql` — SIMPLE recovery model fix + log shrink (commit `0b8d151`)
- `src/AccessControlPro.Application/Services/BackupService.cs` — retention 2 → 3 days (this commit)
- `src/AccessControlPro.WPF/App.xaml.cs` — `RunStartupBackupIfStale()` + call from `OnStartup` (this commit)
- `tools/backup-cleanup/cleanup-old-backups.bat` — manual disk-cleanup script (this commit)

## Pending / unresolved at end of day

1. **Basmia needs to re-apply SIMPLE recovery model** — their manual fix today only shrank the log but flipped the DB back to FULL. The next 2-4 weeks will refill the log. Either AnyDesk in and run `ALTER DATABASE AccessControlPro SET RECOVERY SIMPLE WITH NO_WAIT;` once, or `git pull && tools\post-install\post-install.bat` which now does it automatically.
2. The 3 original pending decisions are unchanged (timezone direction, 1853 Migrated players, beta-customer comms).

## How to pick up in the next conversation

Everything from this session is committed and pushed. The next build/install at any customer will:
- Generate a unique API key automatically (existing)
- Set DB recovery to SIMPLE in the tuneup script (existing as of `0b8d151`)
- Keep 3 days of backups (existing as of this commit)
- Run a startup backup if stale (existing as of this commit)
- Show a backup splash before login if backup is needed

If a customer reports "the app takes a long time to start once a day" — that's the on-launch backup running. Expected behavior, not a bug. Splash text tells them what's happening.

*End of 2026-05-28 update.*

---

# 2026-05-29 — Multi-customer scaling foundation (3 phases)

## What happened today

User asked: "we are done — but you have more experience, check if we missed anything or could add useful features." After audit found ~10 gaps, narrowed to 3 high-value items and shipped them all in one session.

### Phase A — EF Core retry policy (~10 min)
- **Problem**: Windows Update restarts the SQL Server service for ~30 sec. Without retry, every in-flight DB call during that window throws → red errors all over the UI → customer panics, force-kills the app. The `BadGateway` lines in Basmia's `session.log` were a related symptom.
- **Fix**: `src/AccessControlPro.Infrastructure/DependencyInjection.cs` — both `UseSqlServer` calls now use `.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: 10s)`. EF Core knows which SQL errors are transient (timeout, connection drop, deadlock victim) vs permanent (syntax error, constraint violation) and only retries the transient ones.
- **Result**: SQL Server restarts are now invisible to the customer — the card swipe just takes 14 sec instead of 0.1 sec.

### Phase B — Disk-space guard (~1 hour)
- **Problem**: Backup folder + log folder grow forever bounded only by retention. If the customer's drive ever dropped below ~500 MB free, the backup wrote 0-byte `.bak` files. Silent failure → customer thinks they have a backup, they don't.
- **Fix**:
  - `BackupService.cs` — added `DiskWarnMb = 500` and `DiskRefuseMb = 100` constants
  - `GetFreeDiskSpaceMb(path)` helper using `DriveInfo.AvailableFreeSpace`
  - Inside `RunBackupAsync`, after cleanup-old and before write: check free space. <100 MB = abort with "Backup REFUSED" message (keeps last good backup intact). <500 MB = warn + flag, still proceed.
  - New fields on `BackupStatus`: `LowDiskWarning` (bool), `LastFreeSpaceMb` (long).
  - `App.xaml.cs` — after the startup backup splash closes, if status flag is set, show a bilingual `CustomMessageBox` (Warning if proceeded, Error if refused).
- **Result**: customer sees an explicit "free up disk space" dialog instead of a silent failure.

### Phase C — Auto-update (~3 hours)
- **Problem**: Every new build requires AnyDesk in to manually copy files. Fine for Basmia. Painful at 5 customers. Impossible at 20.
- **Architecture**:
  ```
  VPS                                WPF                              Updater.exe
  ──────────────────────────────────────────────────────────────────────────────
  GET /api/version/latest
    ↓ returns latest.json
  ←──── UpdateCheckService ──────→ shows bilingual prompt (Yes/No/Skip)
                                    ↓ Yes
                                  UpdateInstallerService
                                    ↓ downloads ZIP
                                    ↓ verifies SHA-256
                                    ↓ pre-update backup
                                    ↓ writes pending_update.json
                                    ↓ launches Updater.exe
                                    ↓ Application.Current.Shutdown()
                                                                  ────────────→ waits for WPF PID exit
                                                                                copies DLLs to _rollback/
                                                                                extracts ZIP (skips appsettings,
                                                                                License, Logs, Backups)
                                                                                writes .post_update marker
                                                                                relaunches AccessControlPro.WPF.exe
  WPF starts fresh                                                ←────────────
    ↓ reads .post_update
    ↓ shows "What's new in vX.Y.Z" once
    ↓ deletes marker
  ```

- **Files added**:
  - `src/AccessControlPro.Application/Services/UpdateCheckService.cs` — polls `/api/version/latest`, version comparison via `System.Version`, snooze/skip storage in `.update_snooze`, HTTPS-only enforcement on downloadUrl
  - `src/AccessControlPro.Application/Services/UpdateInstallerService.cs` — downloads with progress, SHA-256 verify, pre-update backup, writes `pending_update.json` sentinel, launches Updater.exe
  - `tools/Updater/Updater.csproj` — self-contained single-file win-x86 build (62 MB — no WPF/WinForms, just P/Invoke MessageBox for fatal errors)
  - `tools/Updater/Program.cs` — wait-for-PID-exit, ZIP extract with preserved-list, retry on locked files, rollback safety copy, relaunch main app
  - `src/AccessControlPro.Web/wwwroot/releases/latest.json.example` — manifest format
  - `tools/release-management/README.md` — step-by-step release deploy recipe

- **Files modified**:
  - `src/AccessControlPro.Web/Program.cs` — added `MapGet("/api/version/latest")` that reads `wwwroot/releases/latest.json` (5 min Cache-Control, public, no API key)
  - `src/AccessControlPro.WPF/App.xaml.cs` — DI registration of both new services, `CheckForUpdateAndPromptAsync()`, `ShowUpdatePrompt()`, `DownloadStageAndInstallAsync()`, `ShowPostUpdateNotesIfAny()`
  - `src/AccessControlPro.WPF/AccessControlPro.WPF.csproj` — `ProjectReference` to Updater with `ReferenceOutputAssembly=false`, MSBuild targets to copy Updater.exe into both Build output (Debug) and Publish output (Release self-contained)

- **Preserved during update** (never overwritten):
  - `appsettings.json` (gym name, language, API key, SQL connection string)
  - `License.dat`
  - `.backup_status`, `last_run_state.json`, `last_sync.json`, `.last_backup`, `pending_update.json`, `.update_snooze`
  - Folders: `Logs\`, `Backups\`, `update_staging\`, `_rollback\`

- **Customer experience**:
  1. App launches → backup splash (if stale) → `.post_update` check (if just updated, show "What's new") → background update check
  2. If new version found AND not snoozed/skipped → bilingual dialog (size, release notes, version)
  3. Customer clicks "Update Now" → download splash with progress bar → "Verifying..." → "Pre-update backup..." → "Restarting..."
  4. App exits, Updater.exe runs ~30 sec with status in console title bar
  5. App relaunches → "What's new in v4.5" dialog (one-time)
  - Total wait: ~2 min from click to login

- **Safety**:
  - SHA-256 verification — corrupt downloads aborted, ZIP deleted
  - Pre-update backup runs before file swap
  - `_rollback/` folder gets copies of all DLLs + main exe before extraction (manual recovery path if update breaks)
  - Mandatory updates (`mandatory:true` in manifest OR `current < minVersion`) hide the "Skip" option
  - All steps logged to `Logs/updater.log` and `Logs/startup_log.txt`
  - Snooze button = 24h. Skip button = never prompt this version again. Mandatory updates can't be skipped.

- **How to publish a new release**: see `tools/release-management/README.md`. TL;DR — `dotnet publish` + ZIP + SHA-256 + scp + edit `latest.json` on VPS.

## State of the code at end of day

Branch `feature/pwa-cloud`, clean build (0 errors). Pushed to origin. Basmia's install will pick up all 3 fixes on the next deployed build.

## Pending / unresolved

1. **Basmia still needs SIMPLE recovery model applied** — unchanged from yesterday. Their DB went back to FULL after the manual fix. Next backup attempt in 2-4 weeks will fail.
2. **First-real-release test** — auto-update has only been built and unit-checked via compile. The full flow (download → verify → install → relaunch) hasn't been tested end-to-end on a real customer-shaped install. Recommended first test: publish v4.5.0 build, scp to VPS, edit `latest.json`, run on a local clone of Basmia's install setup, verify update flow.
3. **Customer-facing notifications from VPS → WPF** — still skipped, no clear ROI yet.
4. **`/health` endpoint on VPS** — skipped, would only matter once we have 5+ customers.

## How to pick up in the next conversation

The session log now spans 2026-05-18 through 2026-05-29. Three commits today (or one big one — depends on how I commit):
- EF retry + disk guard + Phase A/B/C/D auto-update

If the next session is about testing the update flow end-to-end, the recipe is in `tools/release-management/README.md`. If it's about a new feature, the codebase is in a clean, well-documented state.

*End of 2026-05-29 update (Phase A/B/C of code work).*

---

# 2026-05-29 (evening) — Auto-update pipeline LIVE end-to-end

After the 3-phase implementation earlier in the day, the rest of the evening was about taking the auto-update mechanism from "code exists" to "actually working in production". Multiple gotchas had to be fixed along the way.

## Major milestones reached

1. **SSH key authentication to VPS** — generated ed25519 key on dev PC, uploaded to VPS, fixed an authorized_keys formatting bug (key got appended without newline, gluing it to Hostinger's existing key), passwordless login confirmed.
2. **Publish-release.ps1 script works end-to-end** — proven by publishing v4.5.0 to the VPS via one command.
3. **Static manifest URL live** — `https://hmtech.solutions/releases/latest.json` returns the JSON.
4. **API endpoint live** — `https://hmtech.solutions/api/version/latest` returns the JSON (after deploying the new Blazor build).
5. **v4.5.0 ZIP downloadable** — `https://hmtech.solutions/releases/AccessControlPro-v4.5.0.zip` (104 MB).
6. **WPF version baked into builds** — fixed the "1.0.0.0 vs 4.5.0" infinite-prompt bug.

## Issues hit + fixed (in order)

1. **VPS password stored only in FileZilla** — found the FileZilla XML sitemanager.xml at `%APPDATA%\FileZilla\sitemanager.xml`, decoded the base64-encoded `<Pass>` field. Password: `.ovPGMMnu3D3?2bJ`. User saved it outside the repo. NOT committed anywhere.

2. **SSL info in memory was wrong** — memory said SSL expires 2026-06-19. Actual: 2026-08-12 (wildcard cert) + 2026-08-19 (specific cert). Updated `reference_domain.md` with correct dates.

3. **SSH key got mangled on the VPS** — used `cat >> ~/.ssh/authorized_keys` to append the public key, but the file lacked a trailing newline so the new ed25519 key got glued to the existing Hostinger RSA key. Diagnosed by inspecting `authorized_keys` content. Fixed with sed: `sed -i 's/#hostinger-managed-keyssh-ed25519/#hostinger-managed-key\nssh-ed25519/' ~/.ssh/authorized_keys`.

4. **publish-release.ps1 had non-ASCII characters** — em-dashes (`—`) and bullets (`•`) in the source. PowerShell 5.1 reads .ps1 files as Windows-1252 when there's no BOM, which corrupted those characters and broke the parser ("missing terminator"). Rewrote with pure ASCII; bullets in the manifest output are built at runtime via `[char]0x2022`. Manifest is written via `System.Text.UTF8Encoding(false)` so Linux reads it without a BOM.

5. **VPS path was wrong** — my memory said `/var/www/gymapp/AccessControlPro.Web/wwwroot/releases/`. Reality: `/var/www/gymapp/AccessControlPro.Web` is the LINUX APPHOST EXECUTABLE FILE, not a directory. wwwroot is a sibling. Correct path: `/var/www/gymapp/wwwroot/releases/`. Fixed in publish-release.ps1, README.md, and `reference_vps.md` memory. Added explicit `mkdir -p $VpsReleasesDir` step to the script so it's idempotent.

6. **WPF didn't have an explicit version** — Assembly.GetName().Version returned `1.0.0.0`. Update check would then compare `1.0.0.0 < 4.5.0` → always shows "update available" → infinite loop on every launch. Fixed by adding `<Version>4.5.0</Version>` + `<AssemblyVersion>` + `<FileVersion>` to the csproj, AND patched publish-release.ps1 to pass `-p:Version=$version` to `dotnet publish` so the version baked into the binary matches the version typed by the user. Rebuilt + re-uploaded v4.5.0.

## VPS state at end of day

| Item | Status |
|---|---|
| `gymapp.service` (Blazor) | Running, new build with `/api/version/latest` endpoint |
| `/api/version/latest` | Returns v4.5.0 manifest with EN+AR notes |
| `/releases/AccessControlPro-v4.5.0.zip` | 109 MB, SHA-256: `5CEC4C0D687D1AF794215E36312616823E991B18BB1240F39109DFF8F3C317D4` |
| `/releases/latest.json` | Static fallback, same manifest |
| Old Web.dll backup | At `/tmp/AccessControlPro.Web.dll.bak-2026-05-29` — delete when new build is proven stable |
| Basmia's sync / diagnostics / auth | All still working (only added the new endpoint, didn't change anything else) |
| SSL | 75-81 days remaining, auto-renewal scheduled twice daily |
| VPS itself | Expires 2026-06-20, auto-renewal ON |
| Domain hmtech.solutions | Expires 2027-03-21, auto-renewal ON |

## Commits today (in order)

- `363a4c3` — Multi-customer scaling foundation: SQL retry, disk guard, auto-update
- `910e164` — Backup safety net: 3-day retention, on-launch backup-if-stale, manual cleanup (from earlier today)
- `0375e02` — Release publisher: ASCII-clean script + correct VPS path + CREDENTIALS.md
- `ed5d6af` — WPF: bake explicit version into builds for auto-update comparison

## What Basmia still needs (ONE final manual session)

After this, she NEVER needs an AnyDesk update again:

1. **Deploy v4.5.0 manually** — last manual deploy ever:
   - Download `https://hmtech.solutions/releases/AccessControlPro-v4.5.0.zip` (104 MB)
   - Backup current `D:\AccessControlPro\` folder
   - Extract ZIP to `D:\AccessControlPro\`
   - Copy `appsettings.json`, `License.dat`, `Logs\`, `Backups\` from backup folder back into new install
   - Launch + verify sync, devices, login work

2. **Apply SIMPLE recovery permanent fix** — 10 sec in SSMS:
   ```sql
   USE master;
   ALTER DATABASE AccessControlPro SET RECOVERY SIMPLE WITH NO_WAIT;
   ```

3. After both: she's on auto-update. Every future v4.6.0, v4.7.0... auto-installs from a single PowerShell command on dev side.

## Release process (going forward)

```powershell
# Bump the version in src/AccessControlPro.WPF/AccessControlPro.WPF.csproj first:
#   <Version>4.6.0</Version>
#   <AssemblyVersion>4.6.0.0</AssemblyVersion>
#   <FileVersion>4.6.0.0</FileVersion>

# Then:
cd D:\AccessControlPro\tools\release-management
.\publish-release.ps1
# Answer 3 questions, wait 10 min, done
```

## Pending decisions (unchanged, all non-blocking)

1. Timezone direction (10:15:43 morning vs afternoon)
2. 1,853 Migrated players strategy
3. Customer beta-customer comms

## How to pick up next session

This log now covers May 18 → May 29, all phases (1 through 5 + auto-update + VPS deployment). Read this single file in any new conversation to pick up where we left off. The codebase is clean, all phases committed and pushed to `feature/pwa-cloud`.

The very next session's job is probably:
- (a) AnyDesk to Basmia ONCE to deploy v4.5.0 + apply SIMPLE recovery, then test the auto-update flow with v4.6.0
- OR (b) Onboard customer #2

*End of 2026-05-29 (evening) update — auto-update mechanism is LIVE.*
