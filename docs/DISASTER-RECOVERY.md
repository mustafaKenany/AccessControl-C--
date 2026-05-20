# Disaster Recovery Runbook

What to do when something on a customer's setup catastrophically fails.

## Scenarios in rough order of likelihood

### 1. SQL Server stops working

**Symptoms**: app shows "Database connection lost" on every action; nothing saves; clicks don't work.

**Investigation** (1 min):
```cmd
sc query MSSQLSERVER
```

**Fix attempts** in order:
1. `net start MSSQLSERVER` — start the service
2. If "service refuses to start" → check Event Viewer → Application log for SQL Server errors
3. If "service started but app still fails" → check `appsettings.json` connection string is correct
4. If "still broken" → reinstall SQL Server Express, restore from latest `D:\Backups\AccessControlPro\*.bak`

### 2. Customer's PC SSD dies / Windows won't boot

**Recovery path** (~2-3 hours):
1. Source: latest backup `.bak` file. Hopefully synced to cloud-deploy, AND saved on a USB stick the customer keeps off-PC.
2. New PC + fresh Windows install
3. Install SQL Server Express (same edition as old)
4. Restore the `.bak`:
   ```sql
   RESTORE DATABASE [AccessControlPro]
       FROM DISK = N'D:\Backups\Restored.bak'
       WITH REPLACE,
            MOVE 'AccessControlPro_Data' TO 'C:\SQLData\AccessControlPro.mdf',
            MOVE 'AccessControlPro_Log'  TO 'C:\SQLData\AccessControlPro.ldf';
   ```
5. Install latest WPF build, copy old `appsettings.json` (so CloudApiKey + DB password are preserved)
6. Run `tools/post-install/post-install.bat`
7. Customer launches the app — should see all their players
8. Run a manual cloud sync to verify connectivity

### 3. WPF crashes immediately on launch after an update

**Recovery**:
1. Stop the running process via Task Manager
2. Check `crash_log.txt` for the exception
3. If crash is in startup code → roll back to the previous build (always keep the previous `publish/customer-deploy/main-app/` zipped on a USB stick)
4. Open an issue with the crash log attached

**Mitigation**: Always keep the previous customer build on the install USB stick. Never deploy without keeping the rollback ready.

### 4. Cloud sync stops working after working fine for months

**Symptoms**: `/superadmin/gym-health` shows the gym as "Critical — sync 30+ hours ago".

**Investigation**:
1. SSH into the VPS, check Nginx + gymapp service status
2. Check the cloud `CloudSyncLogs` table for the most recent attempts → look for "Sync API error: ..." messages
3. From the customer's PC, run a manual sync (Help → Force Sync if it exists, or just wait 5 min)
4. Read the customer's local `cloud_sync_log.txt` for the most recent errors

**Common causes**:
- Customer's internet down → not our problem
- VPS out of disk space → `df -h` then clean up `/var/log/journal`
- SSL cert expired → check `certbot renew --dry-run`; renew via DNS challenge
- Customer's API key got out of sync with cloud → compare `appsettings.json` `CloudApiKey` with `Gyms.ApiKey` in master DB

### 5. Hostinger VPS is down

**Impact**: cloud sync queues locally (app keeps working), no web access. Card swipes at the door still work because that's device-local.

**Recovery**:
1. Hostinger admin panel → reboot the VPS
2. After reboot: `systemctl status nginx gymapp postgresql`
3. If PostgreSQL won't start, check `/var/log/postgresql/` for corruption
4. If irrecoverable, restore PostgreSQL from your most recent dump (you DO take dumps, right?)

**Pre-emptive**: nightly pg_dump cron — write the script and add a calendar reminder to verify backups monthly.

```bash
# Add this to /etc/cron.d/postgres-backup:
0 3 * * * postgres pg_dumpall | gzip > /var/backups/postgres/all-$(date +\%Y\%m\%d).sql.gz
0 4 * * * root find /var/backups/postgres -name "*.sql.gz" -mtime +30 -delete
```

### 6. SSL certificate expired

**Symptoms**: customers report "your connection isn't private" in their browser; cloud sync from WPF starts failing.

**Recovery** (~10 min):
```bash
ssh root@89.116.39.155
certbot certonly --manual \
  --preferred-challenges dns \
  --email mustafa.kenany2022@gmail.com \
  --agree-tos --no-eff-email \
  -d "hmtech.solutions" -d "*.hmtech.solutions"
# (Add the TWO TXT records to Hostinger DNS when prompted, wait 2 min, press Enter)
systemctl reload nginx
```

**Pre-emptive**: Calendar reminder 60 days before expiry. Current cert expires **2026-08-12**.

### 7. Customer reports "all their data is gone"

Usually NOT data loss — usually a UI bug or filter issue. Investigate in this order:
1. Are they on the right page? Open Players, clear search, set filter to "All"
2. Are they connected to the right DB? Check `appsettings.json` connection string
3. Did they recently restore a backup that was older than they thought?
4. Check `Employees` table row count via `sqlcmd` — if it's normal, data is fine, just UI confusion
5. If row count is actually zero → restore latest `.bak` immediately

### 8. Cloud DB corrupted / SuperAdmin can't login

**Recovery**:
1. SSH in, check PostgreSQL status
2. Try connecting via `psql -U postgres gymcloud`
3. If table corruption: `REINDEX DATABASE gymcloud;` then `VACUUM FULL;`
4. If credentials corruption: master `Users` table can be reset via SQL:
   ```sql
   UPDATE "Users" SET "PasswordHash" = '<bcrypt-hash-of-known-password>'
   WHERE "Username" = 'admin' AND "Role" = 'SuperAdmin';
   ```

## Always-required backups

| Backup | Where | How often | Verify how |
|---|---|---|---|
| Customer's local SQL DB | `D:\Backups\AccessControlPro\*.bak` | Twice daily (auto, runs at 02:24 and 14:24) | Confirm `backup_log.txt` shows recent successful entries |
| VPS PostgreSQL master+gym DBs | `/var/backups/postgres/*.sql.gz` | Daily (cron) | Spot-check by `gunzip -t` on a recent file |
| WPF build archive | Your dev machine + offsite | Per release | Keep last 3 releases zipped |
| Code | GitHub `feature/pwa-cloud` branch | Every commit | `git log` |
| Customer `appsettings.json` per customer | Your secure password manager | At install time + after any change | Verify against latest backup |

## Tests to run quarterly

- [ ] Restore a customer's `.bak` to a separate test machine. Confirm app starts and shows their data.
- [ ] Take down the VPS for 1 hour. Confirm customer's app keeps working and queues sync, then resumes when VPS comes back.
- [ ] Stop SQL Server on a test PC. Confirm app shows a friendly error (not a crash) and recovers when SQL is restarted.
- [ ] Delete a few cards on a test device, then re-sync from app. Confirm cards return.

## Phone numbers and contacts

- Hostinger support: (look up current — they change it)
- VPS root password: (password manager)
- Domain registrar: Hostinger (whois `hmtech.solutions`)
- SQL Server license: (whichever you bought)
- AccessControlPro device manufacturer: (SDK vendor contact info)

> **Pro tip**: keep this file up to date. The next time something breaks at 2 AM, you'll thank yourself.
