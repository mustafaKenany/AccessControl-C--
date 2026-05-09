# Session Log — 2026-05-09

**Session start:** 2026-05-09 (morning)
**Session end:** 2026-05-09 (evening)
**Branch:** feature/pwa-cloud
**Goal:** Audit all customer log files for issues, build forensic crash logging, refresh customer build, replace landing-page placeholder mockups with real screenshots

---

## Part 1 — Log file audit (12+ files reviewed)

Customer sent the following log files; I read each end-to-end:

| File | Verdict |
|---|---|
| `backup_log.txt` (2026-05-01 → 05-09) | Healthy. Daily backups twice per day, 314 → 317 MB stable growth, zero failures. |
| `card_disable_log.txt` | 9 successes, 1 failure on 2026-05-08 22:16 (Card 2212461, device offline). Worth checking retry behaviour but not urgent. |
| `cleanup_log.txt` | 8 runs, 63 inactive players archived+deleted, 6-month retention applied correctly. |
| `cloud_sync_log.txt` (pre-deploy) | The big one — every sync reported `errors=10694` with `ok=True`. Misleading. Root cause = stale errors from 2026-04-30 full sync, NOT recent activity. |
| `ocsm` / `ocsm_1` … `ocsm_9` | Vendor SDK debug log, CP936-mojibake socket noise, completely useless. |
| SDK wrapper log | Healthy but inefficient — full SDK init cycle repeats per single card add. |
| `app_startup_log.txt` | Many short-lived restarts but no clean exits between them — looked like crashes or force-kills with no way to tell which. |
| `crash_log.txt` (2026-05-03 17:57) | OOM exception inside WPF visual-tree recursion (mouse-hover triggered). Stack frames alternate `CalculateSubgraphBoundsInnerSpace ↔ OuterSpace` — really a stack overflow, masquerading as OOM. Crash log gave no context: which window? which control? what action? |
| `activity.log` (per-user actions) | Excellent diagnostic data. Cross-references perfectly with SDK log (every `AssignCard` matches an `addUnSortCard()` within 1s). One anomaly: 14× `Employees: entered` in 6s on 05-01 08:45 — likely impatient menu clicking, but worth watching for repeats. |

### Key finding from the cloud sync log

The "10694 errors" was a real bug from BEFORE the 2026-05-02 deploy, but the CODE fix was already in the repo (`SyncHelper` savepoints + AsyncLocal per-request error reset, both committed 2026-04-21). The fix was deployed to the VPS on 2026-05-02 (per `2026-05-02_web-deploy-day.md`). The customer log we reviewed was pre-deploy data — no further server fix needed.

---

## Part 2 — Forensic crash logging (the new build)

Goal: when something goes wrong, the crash log should answer "what was the user doing, on which screen, with what mouse position, with what memory pressure" without us having to ask the customer to remember.

### New files (all in `src/AccessControlPro.WPF/Helpers/`)

**`CrashContextLogger.cs`** — replaces the bare exception/stack output in all 3 apps' `App.xaml.cs`. Captures, in addition to the exception:
- Active window + all open windows + focused control + visual-tree ancestors (6 levels)
- Mouse position in the active window
- Working set / private bytes / managed heap / GC counts (gen 0/1/2)
- App version, Windows user, OS, .NET version
- Thread id / name / apartment
- Defensive: every section independently try/catch'd; UI access guarded by `Dispatcher.CheckAccess()` so cross-thread crashes don't recurse

**`LastRunStateTracker.cs`** — writes `last_run_state.json` on startup with `status="running"`, updated to `clean_exit` on `OnExit` or `system_shutdown` on `SessionEnding`. Next startup reads previous state and emits one of:
- `Previous session: clean_exit (started X, ended Y)`
- `Previous session: system_shutdown — Logoff/Shutdown ...`
- `Previous session: KILLED OR CRASHED — process N stopped without clean shutdown ...`
- `Previous session: first_run (no state file)`

Combined with `crash_log.txt` this distinguishes:
- Real crash (KILLED + matching crash_log entry)
- Force-kill / power loss (KILLED + no crash_log entry)
- Windows shutdown (system_shutdown)
- Normal close (clean_exit)

### Modified files

**`ActivityLogger.cs`** — added an in-memory ring buffer of the last 30 entries alongside the existing file write. Crash handler reads the buffer instead of re-reading the file (which may be locked or huge). Public method `GetRecentBreadcrumbs()` returns oldest-first.

**`CrashContextLogger.cs`** then includes those breadcrumbs in every crash entry — so the log shows literally "the user did X, then Y, then Z, then app crashed".

**`App.xaml.cs` (all 3 apps)** — replaced the inline `WriteCrashLog` body with `CrashContextLogger.Write(...)`. WPF App also wires up `LastRunStateTracker` (read previous state at start of `OnStartup`, record clean exit at start of `OnExit`, hook `SessionEnding` for system shutdown), and adds `CleanupSdkDebugLogs()` which deletes any `ocsm*` files in the app folder before the SDK loads (kills the vendor mojibake noise we can't disable any other way).

### Sample crash log entry (new format)

```
=== [...] DispatcherUnhandledException ===
Exception: System.OutOfMemoryException: ...
Stack: ...

--- App context ---
MainWindow: MainWindow "Access Control Pro" Visible=True Active=True
Open windows (3): MainWindow ... / PlayerEditDialog "Edit Player - Ahmed" ...
Focused: Image Name="PlayerPhoto"
Visual ancestors: Image → Border → Grid → DockPanel → PlayerEditDialog
Mouse position (in active window): 412,287

--- Recent user activity (oldest → newest) ---
  [...] [NAV] Employees: entered
  [...] [ACTION] Employees: AssignCard - عيسى محمد عيسى card=2365478
  [...] [NAV] Employees: entered    ← user clicked menu 4× in 1 second
  [...] [NAV] PlayerEdit: entered
  ← crash here

--- Process info ---
PID: 14528, uptime: 02.04:18:31
Working set: 387 MB, private: 412 MB, managed heap: 124 MB
GC collections (gen0/1/2): 1245/87/12
App version: 1.0.0.0, Windows user: Mustafa
OS: Microsoft Windows NT 10.0.19045.0, .NET: 8.0.11
Thread: id=1 name="<unnamed>" apartment=STA
=== end ===
```

### Customer build refresh

Did a clean rebuild + republish for all 3 customer apps. The first parallel-publish attempt hit file-lock contention on shared `obj/` folders, so retried sequentially. Final outputs:

| App | Folder | Size | Entry |
|---|---|---|---|
| Main | `publish/customer-deploy/main-app/` | 194.2 MB | `AccessControlPro.WPF.exe` |
| Admin | `publish/customer-deploy/admin-panel/` | 195.5 MB | `AccessControlPro.Admin.exe` |
| POS | `publish/customer-deploy/pos-terminal/` | 195.2 MB | `AccessControlPro.POS.exe` |

Self-contained x86 builds, no .NET runtime required on customer PC.

### Verified working in production

Customer installed and ran the new build. First two startup-log entries proved both new mechanisms are live:

```
[16:29:23] Previous session: first_run (no state file)         ← fresh install
[16:30:35] Previous session: KILLED OR CRASHED — process 12968
           stopped without clean shutdown ...
```

The "KILLED OR CRASHED" verdict is technically correct but a known false positive on first install — the SetupWizard closes without going through `OnExit`, so the tracker can't tell the difference. Subsequent normal closes will correctly show `clean_exit`. Not fixing for now; cosmetic only.

### Cloud sync — proven fixed end-to-end

Same customer launch, first cloud sync after the new build:

```
Cloud sync completed: ok=True, total=12814, errors=0
```

12,814 records (2,185 players, 2,000 events, 767 transactions, 1,165 deleted, 2,186 cards, 3,500 QR codes) synced in 9 seconds. **errors=0** — no more cascading transaction-aborted ghost errors. The whole pipeline (savepoints + AsyncLocal error list + correct `ok` calculation) works as designed.

---

## Part 3 — Landing page screenshots

Customer sent 12 PNG screenshots from the WPF app. The existing landing page Screenshots section (lines 212–273) was rendering icon-only mockup tiles inside fake browser chrome — looked unfinished.

### What changed

**`LandingPage.razor`** — replaced the 4 placeholder cards with 8 real product screenshots, each in the existing `screenshot-mockup` browser-chrome frame. Captions are bilingual EN/AR and double as marketing copy + documentation. Picked the 8 most product-representative screenshots and skipped the small dialogs (Add Income / Add Expense / Change Password) that don't fit the 16:10 aspect.

| Filename | Section shown |
|---|---|
| `login.png` | HM-GymManagement branded login screen |
| `dashboard.png` | لوحة التحكم — 4 KPI cards |
| `players.png` | اللاعبون — member list with status filters |
| `add-player.png` | إضافة لاعب — registration form |
| `finance.png` | المالية — income/expense/profit KPIs |
| `transactions.png` | الإيرادات والمصروفات — transaction ledger |
| `devices.png` | الأجهزة — controllers list |
| `doors.png` | الأبواب — per-door schedules and status |

**`app.css`** — added `.screenshot-image` rule (16:10 aspect, `object-position: top`) so the actual UI screenshots crop cleanly inside the chrome frame.

Customer needs to drop the 8 PNGs into `src/AccessControlPro.Web/wwwroot/images/screenshots/` (folder created in this commit) before the next Web redeploy.

---

## Part 4 — Side-quest fixes raised but deferred

These came up during the audit and aren't urgent, but are worth a future session:

1. **`appsettings.json` exclusion fix** — same problem hit on 2026-05-02 VPS deploy: dev defaults overwrite production config. Proposed: `<CopyToPublishDirectory>Never</CopyToPublishDirectory>` on all 4 csproj files (WPF, Admin, POS, Web). Removes the entire backup-restore-during-deploy dance permanently.
2. **SDK init per card** — the wrapper calls `Initialize: PreloadNativeLibraries → initNet → Creating ConnectMain` for EVERY single `AssignCard`. ~500 ms wasted per card. Should be initialized once and reused.
3. **Garbage RFID reads filtering** — saw card numbers like `5242882`, `1476395007` in WatchEvents (real cards top out around ~3.7M). RFID antenna noise. Filter by sane upper bound.
4. **First-install false positive in LastRunStateTracker** — SetupWizard closes without OnExit, so first reboot always shows "KILLED OR CRASHED". Cosmetic; could record `clean_exit` from the wizard's close handler.
5. **Bug 2 from 2026-05-02 session** — find the row 786 numeric-overflow Player record in the customer's local DB. With cloud sync now reporting accurate errors, if the row still exists we'll see `errors=1` consistently.

---

## Files touched

### Forensic crash logging commit (88a2791)
- NEW: `src/AccessControlPro.WPF/Helpers/CrashContextLogger.cs`
- NEW: `src/AccessControlPro.WPF/Helpers/LastRunStateTracker.cs`
- MODIFIED: `src/AccessControlPro.WPF/Helpers/ActivityLogger.cs` (breadcrumb buffer)
- MODIFIED: `src/AccessControlPro.WPF/App.xaml.cs` (tracker wiring + ocsm cleanup + use CrashContextLogger)
- MODIFIED: `src/AccessControlPro.Admin/App.xaml.cs` (use CrashContextLogger)
- MODIFIED: `src/AccessControlPro.POS/App.xaml.cs` (use CrashContextLogger)

### Landing page screenshots commit (2cdaed3)
- MODIFIED: `src/AccessControlPro.Web/Components/Pages/LandingPage.razor` (real screenshots, bilingual captions)
- MODIFIED: `src/AccessControlPro.Web/wwwroot/app.css` (`.screenshot-image` rule)
- NEW DIR: `src/AccessControlPro.Web/wwwroot/images/screenshots/` (PNGs to be added by customer)

### Build artifacts (not committed)
- `publish/customer-deploy/main-app/` — 194.2 MB, ready to copy to customer PC
- `publish/customer-deploy/admin-panel/` — 195.5 MB
- `publish/customer-deploy/pos-terminal/` — 195.2 MB

---

## What customer is asked to do next

1. Save the 8 PNG screenshots to `src/AccessControlPro.Web/wwwroot/images/screenshots/`
2. Republish Web project for `linux-x64`, upload to VPS `/var/www/gymapp/`, restart `gymapp` service
3. Send fresh `crash_log.txt`, `app_startup_log.txt`, and `Logs/activity.log` after a few days of normal use to confirm forensic logging picks up real-world events correctly

---

## Lessons noted

- **Reading the audit logs in order matters.** Initial session burned cycles diagnosing what looked like a live cloud sync bug; turned out the customer was sending pre-deploy data and the fix was already in production.
- **Parallel `dotnet publish` to separate output folders still contends on the shared `obj/` folder** for transitively-referenced projects (Application, Infrastructure). Sequential publish is the safe default.
- **WPF stack overflows manifest as `OutOfMemoryException`** in CLR/WPF crash reports. The 50+ alternating `CalculateSubgraphBoundsInnerSpace ↔ OuterSpace` frames were the giveaway — not the exception type.
