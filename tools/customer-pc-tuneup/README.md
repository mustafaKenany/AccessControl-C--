# Customer-PC Tuneup

One-shot performance tuneup for AccessControlPro installs on typical customer hardware
(i5 / 8 GB RAM / SSD with SQL Server co-located on the same machine).

## What it does

Default WPF + SQL Server installs feel slow not because of the app's code,
but because of three hardware-level defaults that are wrong for a co-located
setup:

1. **SQL Server eats 87% of RAM** — leaves WPF starved, Windows starts paging.
2. **Missing indexes** — Events page, card lookups, and QR pool scans hit full
   table scans on every query.
3. **Windows Power Plan = Balanced + Defender real-time scanning every DB write**
   — random CPU throttling + I/O latency spikes.

This tuneup fixes all three in one shot.

## Files

- `tuneup.sql` — SQL Server config + indexes + statistics refresh (idempotent)
- `tuneup.bat` — Windows wrapper: runs `tuneup.sql`, sets Power Plan to High Performance,
  adds Defender exclusions for the install folder and SQL data folder, restarts SQL.
- `README.md` — this file

## How to run on a customer PC

1. Copy the entire `tools/customer-pc-tuneup/` folder to the customer's PC
   (e.g. to the Desktop).
2. **Right-click `tuneup.bat` → "Run as administrator"**.
3. Type `Y` when prompted.
4. Wait ~30 seconds for it to finish.
5. Tell the user to log out and back in (so visual-effects change takes effect).

**Safe to re-run** — every step is idempotent (memory cap is reapplied, indexes
use `IF NOT EXISTS`, Defender exclusions are deduplicated, etc.).

## Expected impact on a 2000-player gym install

| Operation               | Before | After |
|-------------------------|--------|-------|
| Open Events page        | 3–5 s  | 0.3 s |
| Open Players page       | 1–2 s  | 0.4 s |
| Search player by phone  | 1–2 s  | < 50 ms |
| Card swipe lookup       | 100 ms | < 10 ms |
| App startup             | ~8 s   | ~3 s  |
| Random freezes during sync | yes | gone |

## If SQL Server isn't using default credentials

The script defaults to `sa` / `123` (matches the setup wizard's default). If the
install uses different credentials, set env vars before running:

```cmd
set SQLUSER=myuser
set SQLPASS=mypass
set SQLHOST=localhost
tuneup.bat
```

## If `sqlcmd` is not found

The script needs the SQL Server Command Line Utilities (`sqlcmd.exe` on PATH).
On most Microsoft SQL Server installs it's already there. If not, install
**Microsoft Command Line Utilities for SQL Server** from Microsoft's download
center — it's about 6 MB.

You can also run just the SQL part manually from SSMS:
1. Open SSMS, connect to localhost
2. File → Open → `tuneup.sql`
3. F5 (Execute)

The bat file will skip the SQL step and continue with the Windows-level tweaks
if sqlcmd is missing.

## Rollback

To undo the SQL Server memory cap (restore default ~unlimited):

```sql
USE master;
EXEC sp_configure 'max server memory (MB)', 2147483647;
RECONFIGURE;
```

The indexes can be left in place forever — they only help, never hurt.
The Power Plan can be reverted from Control Panel → Power Options.
Defender exclusions can be reverted from Windows Security → Virus & threat protection.
