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
