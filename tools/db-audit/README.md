# Cloud DB Forensic Audit

Comprehensive read-only audit of the cloud PostgreSQL database
(`gymcloud_drag` on Hostinger VPS) to surface data inconsistencies,
logical errors, and migration leftovers.

## What the script checks

| Section | Checks |
|---|---|
| 1. Cards & Players | orphan cards, recoverable orphans (matchable via /10 rule), duplicate cards, players without cards, players with multiple cards |
| 2. Access Events | total count, unmatched (CardId=NULL), future-dated events, ancient events, orphaned door references, age distribution |
| 3. Players / Subscriptions | total players, still-Migrated flagged records, invalid date ranges, expired-but-active, zero fees, overpaid, unknown subscription types |
| 4. Finance / Transactions | total count, uncategorised, zero/negative amounts, future-dated transactions |
| 5. QR Pool | distribution by status, assigned-without-timestamp, invalid validity window, stale-assigned |
| 6. Cloud Sync Health | sync attempts by status (last 7d), recent failures, gap detection in last 30 days |
| 7. Audit Logs | total count, most-frequent actions, anonymous actions |
| 8. Referential Integrity | dangling card→employee refs, dangling txn→employee refs, lost tombstones |

Every section prints a count + up to 5 example rows.
**Count = 0 → healthy.** Count > 0 → investigate.

## How to run

### On the Hostinger VPS

1. Copy `audit.sql` to the VPS (FileZilla/WinSCP/scp). Suggested location: `/root/audit.sql`.

2. SSH in and run:
   ```bash
   ssh root@89.116.39.155
   sudo -u postgres psql gymcloud_drag -f /root/audit.sql > /root/audit_results.txt 2>&1
   ```

3. Download `/root/audit_results.txt` back to your PC and paste the contents to Claude for analysis.

### Interactive variant

If you prefer running it section by section:
```bash
sudo -u postgres psql gymcloud_drag
\i /root/audit.sql
```

Or copy-paste individual `SELECT` blocks from the file.

## Read-only & safe

Every statement is `SELECT`-only — no `INSERT`, `UPDATE`, `DELETE`, `ALTER`.
You can run it on production without risk.

## After analysis

Once Claude reviews the output, the typical follow-ups are:

- **Cleanup migration** for any orphan AccessCards that map to a registered player via the /10 rule (merge into one card row).
- **Backfill migration** for players still flagged as `Migrated` (force-update phone/subscription/fees from the WPF Edit dialog).
- **Hotfix** if any logical bug is discovered (e.g., future-dated events → timezone bug).
- **Cloud sync gap** investigation if section 6c shows long gaps.

We won't run any DML until you've seen the findings and approved.
