# Data Integrity Cleanup

One-shot SQL cleanup for legacy garbage rows in a customer's local AccessControlPro database. Safe to run, idempotent, backed up automatically.

## Why this exists

The deep audit of Basmia's database on 2026-05-09 found:
- **32 dangling AccessCard rows** (EmployeeId pointing to deleted players)
- **1 duplicate AccessCard** with CardNumber `02190620`
- Various small inconsistencies (empty card numbers, orphan freeze rows)

These don't crash the app, but they cause:
- "Card not found" errors for active players
- Confusing duplicates in the cards UI
- Wrong stats on dashboards
- DB bloat that compounds over time

Same patterns will exist on every new customer install — they come from app crashes mid-operation, force-closes, and historical imports.

## What it does

| # | Cleanup | Action | Why |
|---|---|---|---|
| 1 | Dangling AccessCards | DELETE | Cards pointing to deleted players — junk |
| 2 | Duplicate AccessCards (same CardNumber) | DELETE older, keep newest | Per `CreatedAt DESC, Id DESC` order |
| 3 | Empty CardNumber rows | DELETE | Junk from crashes mid-`AssignCard` |
| 4 | Orphan FreezeHistories | DELETE | Freeze rows for deleted players |
| 5 | Orphan Transaction refs | UPDATE NULL | Detach FK — **transactions themselves are NEVER deleted** (accounting) |

## What it does NOT do

- **Does NOT delete "Migrated" player rows** (1,853 on Basmia). Those carry real card numbers and partial history; deleting them would lock real members out. They clean naturally as the receptionist renews each one (renewal flow forces re-entry).
- Does NOT touch any Employee/Player rows.
- Does NOT touch any audit logs.

## How to run

**On the customer's PC, once after install:**

1. Copy this whole folder to the customer's PC (USB / RDP / FileZilla)
2. Right-click `cleanup.bat` (no admin needed — SQL Server login handles access)
3. Press **Y** at the confirmation prompt
4. The script will:
   - Take a safety backup → `D:\Backups\AccessControlPro\PreCleanup_<timestamp>.bak`
   - Run the cleanup SQL
   - Print before/after counts
5. Keep the backup file for ~7 days, then delete it

## Default credentials

Same as the tuneup script: defaults to `sa / 123 / localhost / AccessControlPro`. Override via env vars:

```cmd
set SQLPASS=their-real-password
set SQLDB=their-db-name
cleanup.bat
```

## Rollback

If the cleanup did something unexpected:

```cmd
sqlcmd -S localhost -E -Q "RESTORE DATABASE [AccessControlPro] FROM DISK=N'D:\Backups\AccessControlPro\PreCleanup_<timestamp>.bak' WITH REPLACE"
```

(The backup file path is printed at the end of the bat output.)

## Verifying it worked

Run the auditor queries from `tools/db-audit/audit.sql` — counts should be zero for dangling cards, duplicates, and orphans.
