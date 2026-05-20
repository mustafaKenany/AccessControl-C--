# Performance Benchmarks

Measured on Basmia's actual hardware/data after the 2026-05-20 stability update. Use these as the **expected baseline** when setting customer expectations or diagnosing complaints.

## Target hardware (typical customer install)

| Component | Spec |
|---|---|
| CPU | Intel Core i5 (6th gen or newer) |
| RAM | 8 GB DDR4 |
| Storage | 256 GB SSD (NVMe preferred) |
| OS | Windows 10/11 (64-bit) |
| Database | SQL Server 2019/2022 Express (with 2 GB max memory cap applied via `tools/customer-pc-tuneup`) |
| Network | 10 Mbps+ internet for cloud sync |

This is **co-located** — both the WPF app and SQL Server run on the same machine. Customer doesn't have a separate DB server.

## Data size at benchmark time (Basmia — typical mid-size gym)

| Table | Rows |
|---|---|
| Players (Employees) | ~2,200 |
| AccessCards | ~2,200 |
| AccessEvents | ~50,000+ |
| Transactions | ~800 |
| AuditLogs | ~1,000 |
| QrPool | 3,500 |
| DeletedEmployees | ~1,100 |
| DB file size | ~320 MB |

## App startup

| Phase | Time |
|---|---|
| Cold start (first launch of day) | 5–8 s |
| Warm start (relaunch within an hour) | 3–5 s |
| To login screen visible | < 2 s after launch |
| To main dashboard after login | < 1 s |

If startup takes > 15 s, check:
- SQL Server status (it may be starting too)
- Daily auto-backup is running (delays migration check)
- Antivirus is scanning the app folder (add Defender exclusion via `tuneup.bat`)

## List page load times

After the 2026-05-20 virtualization fix, these are independent of total row count:

| Page | Load time | Memory footprint |
|---|---|---|
| Players (2,200 rows in DB) | < 0.5 s | ~20 row visuals materialized |
| Events (50,000 rows in DB) | < 0.5 s | ~20 row visuals materialized |
| Logs (1,000 rows in DB) | < 0.5 s | ~20 row visuals materialized |
| Deleted Records (1,100 rows) | < 0.5 s | ~20 row visuals materialized |
| Devices | < 0.2 s | ~1 row |
| Doors | < 0.2 s | ~4 rows |

**Before virtualization** (legacy / pre-fix): Players page took 3–5 s and held all 2,200 row visuals.

## Operations

| Operation | Time |
|---|---|
| Search by phone (with index) | < 50 ms |
| Search by card number (with index) | < 50 ms |
| Add player | < 200 ms |
| Assign card to player (incl. push to device) | 500–1500 ms (depends on device RTT) |
| Renew subscription | < 300 ms |
| Delete player (incl. device card removal) | 500–1500 ms |
| Open Events page (with date filter) | < 500 ms |

## Memory budget

After the 2026-05-20 fixes (virtualization + GC nudge + native SDK init-once):

| State | Working set |
|---|---|
| Idle, freshly launched | 200–300 MB |
| Actively used for 4 hours | 300–400 MB |
| Actively used for 24 hours | 350–450 MB |
| Actively used for 7 days | 400–500 MB |

**Soft alert thresholds** (auto-logged to `memory_log.txt`):
- `>500 MB` warn — "consider restarting today"
- `>700 MB` error — force Gen2 compacting GC

**Before fixes**: working set grew to **681 MB at 3.5 days uptime** and crashed with OOM (this is the Basmia 2026-05-18 incident).

## Cloud sync

| Sync type | Frequency | Payload | Wire time |
|---|---|---|---|
| Initial full sync | Once on first launch after install | ~4-5 MB JSON → ~400 KB compressed | ~10 s |
| Delta sync | Every 5 minutes | ~1-20 KB JSON → ~1-2 KB compressed | < 1 s |
| Pull users + QR | Every 5 minutes (piggy-backed on push) | Usually 0 rows | < 500 ms |

A typical full day of sync transfers under 5 MB total. Easily handled by even a slow rural connection.

## Background tasks (no UI impact)

| Task | Interval | Duration |
|---|---|---|
| Cloud sync | 5 min | < 1 s |
| Memory monitor sample | 10 min | < 5 ms |
| Restart-recommended check | 30 min | < 1 ms (no-op until 5d uptime) |
| Daily inactive-player cleanup | Once at app start | < 5 s (deletes ~5-10 players) |
| DB backup | 12 h (auto) | 2-3 s |
| Diagnostics auto-upload check | 2 min after launch | < 1 s (no-op unless 15d due) |
| QR pool device sync | 12 h (only on day 1 + 15) | ~10 s (warns user) |

## When things are slower than these numbers

| Slow operation | Most likely cause |
|---|---|
| Players page load > 1 s | DataGrid virtualization broken (custom template?) — check `EmployeesView.xaml` panel template |
| Card lookup > 100 ms | Missing index on `AccessCards.CardNumber` — re-run `tuneup.sql` |
| Sync > 5 s | Customer's internet slow OR cloud VPS overloaded — check VPS load |
| Anything > 3× expected | SQL Server starved for RAM — check `tuneup.bat` was actually run (memory cap = 2 GB) |

## How to re-measure on a new customer install

After 7 days of normal usage:
1. Click "Send Diagnostics" in the WPF app
2. On the cloud, open the bundle
3. Read `memory_log.txt` — working set should stay under 500 MB
4. Read `cloud_sync_log.txt` — every sync should be `Synced: ok=True`
5. Read `db-snapshot.json` — confirm row counts roughly match expectations for the gym's size

If anything is off, the diagnostics bundle has every clue needed for support.
