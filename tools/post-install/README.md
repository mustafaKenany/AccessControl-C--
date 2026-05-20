# Post-Install Setup

One script that wraps the two existing post-install steps. Replaces having to run `tuneup.bat` and `cleanup.bat` separately.

## What it does

Calls these two scripts in sequence:

| Step | Script | What it does |
|---|---|---|
| 1 | `../customer-pc-tuneup/tuneup.bat` | SQL memory cap + indexes + Windows power/defender/visual tuning |
| 2 | `../data-integrity/cleanup.bat` | Removes dangling cards, duplicates, orphan rows |

Both are idempotent. Both take their own safety backup before any destructive change.

## How to run

**On the customer's PC, once after install:**

1. Copy this whole `tools/` folder to the customer's Desktop
2. Right-click `tools/post-install/post-install.bat` → **Run as administrator**
3. Press **Y**
4. Wait ~2 minutes
5. Log out and back in (visual-effects setting needs a fresh session)

That's it. The customer-pc-tuneup and data-integrity folders need to be siblings of this folder (which they are if you copy the whole `tools/` tree).

## Why it's a wrapper, not a single script

Keeping the two underlying scripts independent lets you also run them in isolation when needed:
- Only need to re-cleanup data? Run `data-integrity/cleanup.bat`
- Only need to re-apply SQL memory cap after a SQL Server reinstall? Run `customer-pc-tuneup/tuneup.bat`

The wrapper just removes the "do you run both or just one?" decision for the common install case.

## When to NOT run this

- The app is currently running — close it first
- The PC is mid-backup (the daily auto-backup at 02:24 and 14:24) — wait 5 minutes
- SQL Server is not running — check `services.msc` first
