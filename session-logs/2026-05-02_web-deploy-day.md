# Session Log — 2026-05-02 (Web Deploy Day)

**Branch:** feature/pwa-cloud
**Follows:** [2026-04-22_final-polish-and-regressions.md](2026-04-22_final-polish-and-regressions.md)

This is the day everything came together: customer apps redeployed with the EF trigger fix, web portal deployed to VPS, all bugs squashed, customer's full data (2440+ players) finally synced to cloud.

---

## Activities timeline

### Morning — customer app trigger fix
- Customer hit error: "Could not save changes because the target table has database triggers"
- Root cause: my v4.5 migration added triggers but I forgot to register them with EF Core via `HasTrigger()`
- Fixed in `AppDbContext.cs` — registered all 6 trigger-bearing tables (Employees, AccessCards, Users, FreezeHistories, Products, PosShifts)
- Committed `885ed2a`, rebuilt all 3 publish folders, customer redeployed via AnyDesk

### Midday — clean rebuild + final publish
- `dotnet clean` + delete all `bin/obj` + `dotnet restore` + `dotnet build`
- 3 customer apps published to `publish/customer-deploy/`:
  - `main-app/` (196 MB) — WPF main app
  - `admin-panel/` (197 MB) — Admin
  - `pos-terminal/` (197 MB) — POS
- All self-contained x86 builds — no .NET install needed on customer PC

### Afternoon — web deploy to VPS
- Web project published for `linux-x64` → `publish/web-deploy/` (107 MB)
- Workflow on VPS: `systemctl stop gymapp` → `rm -rf /var/www/gymapp/*` → FileZilla upload → `chmod +x AccessControlPro.Web` → `systemctl start gymapp`
- Initial deploy failed — login showed "Invalid username or password"

### Diagnosis of login failure
**Root cause:** the `appsettings.json` in my publish folder had **dev defaults** (`Database=gymcloud`), but the customer's data lives in **`gymcloud_drag`**. The `rm -rf` deleted the production appsettings.json and the upload replaced it with dev one.

**Evidence found via SQL:**
- VPS had two databases: `gymcloud` (master) and `gymcloud_drag` (basmia gym)
- `Gyms` table in gymcloud showed: `Id=10, Name=basmia, DatabaseName=gymcloud_drag, ApiKey=HMT-DRAG-82F1D009`
- `gymcloud.Users` had `admin` user but `gymcloud_drag.Users` was empty
- The owner login flow uses `FindUserDatabaseAsync` which only checks gym DBs registered in the master `Gyms` table — not the master DB itself

**Fix:**
1. Edited `/var/www/gymapp/appsettings.json` to point `CloudConnection` at `gymcloud_drag` instead of `gymcloud`
2. Inserted `admin` user into `gymcloud_drag.Users` (copied hash from `gymcloud.Users`)
3. Restarted service → login worked

### Errors panel investigation
After login worked, dashboard showed alert: "282 sync attempt(s) had errors in the last 24 hours" with old transaction-cascade messages.

**Investigation via `CloudSyncLogs` query:**
- Errors were all from BEFORE today's deploy
- Last 8 syncs (post-deploy) all `Status = Success`
- New SyncHelper savepoints + ON CONFLICT upserts working perfectly

**Cleanup:** marked 3780 old `PartialSuccess` entries as `OldError`, then deleted them entirely from `CloudSyncLogs`. Dashboard alert disappeared.

### Schema fix on gymcloud_drag
Realized the `ALTER TABLE Players ALTER COLUMN Height/Weight TYPE DECIMAL(10,2)` migration runs against the master DB only — never reached `gymcloud_drag`. Manually applied:
```sql
ALTER TABLE "Players" ALTER COLUMN "Height" TYPE DECIMAL(10,2);
ALTER TABLE "Players" ALTER COLUMN "Weight" TYPE DECIMAL(10,2);
```
Defensive; in case any new player data hits the old DECIMAL(5,1) ceiling.

### Force Full Sync — closing the player gap
Customer's local SQL Server had ~2440 players. Cloud showed only 4 — the rest were stuck because the original cascade bug killed full syncs, and delta sync only sends rows changed since `lastSyncAt` (which the old failed syncs had bumped past everyone).

**Fix:** clicked the **Force full sync** button on the dashboard (the new feature I built yesterday).
- Sets `Gyms.ForceFullSync = TRUE` in master DB
- Customer's WPF polls `/api/sync-control` on next sync
- Sees flag, calls `SyncStateManager.Reset()`, runs full sync
- All 2440 players propagate to cloud
- Server's new SAVEPOINT logic isolates any genuinely-bad rows (only truly-malformed Height/Weight rows fail individually)

After sync: cloud now shows real player count matching local. ✅

### Discussed future feature — log sync
User asked about syncing customer log files to cloud. Recommended:
- **NOT continuous** (privacy/PII, bandwidth, GDPR concerns)
- **Approach A** — "Send Logs to Support" button in WPF app (one-click, customer-consented upload)
- **Approach B** — Auto-upload `crash_log.txt` only when crashes occur (no PII, low volume)
- Combined A+B = 95% of diagnostic value with 5% of risk

Deferred to a future session.

---

## Final state

### What's deployed
| Component | Where | Status |
|---|---|---|
| WPF main app | Customer PC | ✅ Running, all bugs fixed |
| Admin panel | Customer PC | ✅ Running |
| POS terminal | Customer PC | ✅ Running |
| Web portal | hmtech.solutions VPS | ✅ Running, login works |
| Cloud DB | gymcloud_drag | ✅ Schema fixed, has 2440+ players |

### Branch state
8 commits today + previous days, all pushed to `origin/feature/pwa-cloud`:
- `2f2cd85` — review + Force Full Sync + mobile
- `104403a` — centralized auth + hardening
- `4034868` — forwarded headers + BCrypt path
- `63c1177` — session store rework
- `d01ebac` — single-instance fix
- `c1c79bc` — middleware order + impersonation
- `5e9d751` — session log
- `885ed2a` — EF Core trigger registration ← today's headline fix
- (this commit) — today's session log

### Build: 0 errors

---

## Lessons learned (for next deploy)

1. **Always backup `appsettings.json` BEFORE `rm -rf`** on VPS deploy
   ```bash
   cp /var/www/gymapp/appsettings.json /tmp/backup.json
   # ... deploy ...
   cp /tmp/backup.json /var/www/gymapp/appsettings.json
   ```

2. **Schema migrations need to run on per-tenant DBs too**
   `DbHelper.InitializeDatabaseAsync` runs only on the master connection. Per-gym DBs need their own migration path. Future improvement: iterate over `Gyms` table and apply migrations to each.

3. **EF Core triggers are a fragile pairing**
   Adding triggers via raw SQL migrations means `OnModelCreating` MUST register them with `HasTrigger()` or all saves break. Keep both in sync or generate the registration list automatically.

4. **Force Full Sync is the cure for stuck delta state**
   Whenever cloud and local diverge (after migrations, after errors, after restoration), this button is the recovery tool. The flag-poll-reset pattern works as designed.

---

## Outstanding (future sessions)

- **"Send Logs to Support" button** in WPF (Approach A from log sync discussion)
- **Auto-upload `crash_log.txt`** (Approach B)
- **Per-tenant migration runner** so future schema changes apply to all gym DBs automatically
- **Customer logs check-in** — wait the planned 2-3 days, collect logs, verify the single-instance fix actually reduced restart count
- **Bug 2 cleanup** — find and fix the bad row 786 Height/Weight value in customer's local DB (now that sync works, the bad row will keep showing as a single isolated error)
