# QR 24-bit Fit Test

This is a one-shot diagnostic to confirm the WG34 facility-byte truncation
theory before changing the global pool range. **No code changes** — just
inserts one extra row in the QrPool table.

## What it tests

The current pool starts at `50,001,001`. As 32-bit hex `0x02FAF49A`, that
overflows WG34's 24-bit card field — the high byte (`0x02 = 2`) ends up in
the facility-code position and the controller logs `Card = 2`.

A 7-digit code like `10,000,001` (hex `0x00989681`) has a zero high byte,
so the controller should log the full number.

## Steps

### 1. Insert the test code

Run the SQL on the customer's local SQL Server. Two options:

**SSMS (graphical):** open `insert_test_pool_code.sql`, connect to the gym
database (default `AccessControlPro`), press F5.

**sqlcmd (command line):** from this folder run
```
sqlcmd -S localhost -d AccessControlPro -E -i insert_test_pool_code.sql
```
Add `-U sa -P yourpassword` if you don't use Windows auth.

You should see `Test pool entry inserted: code=10000001 guest=WG34_TEST` and
one row returned.

### 2. Push it to the device

The QR Pool sync runs automatically every few minutes. To force it sooner:

- restart the WPF app, OR
- click any action that calls `SyncQrPoolToDeviceAsync` (depends on UI; the
  app usually does it on startup and on a timer).

Watch `cloud_sync_log.txt` or the SDK wrapper log for an
`addUnSortCard()... card 10000001` line — that confirms the controller now
has the test card stored.

### 3. Display + scan the QR

In the WPF app:
1. Open **QR Daily Pass** page
2. Find the row with guest name `WG34_TEST` and code `10000001`
3. Click the QR icon (action column) to open the QR Code dialog
4. Hold the QR in front of the gym's QR reader

### 4. Read what the controller logged

Look at the **Real-Time Monitor** window (or `Logs/activity.log`). The
WatchEvent for the scan should show:

| Outcome | What it means |
|---|---|
| `Card = 10000001` | ✅ Theory confirmed. Safe to change pool range globally. |
| `Card = 0` (or some 7-digit number close to 10000001) | ✅ Almost certainly confirmed — controller may strip leading zeros or rearrange bytes, but the QR-reader is delivering the full value now. Worth investigating but the change is still the right direction. |
| `Card = 2` (or any other ≤ 255 value) | ❌ Theory wrong — facility-byte issue isn't the only problem. Investigate WG34 bit layout and reader settings further before changing the range. |
| `Card Not Found` for a different number | Useful clue — the actual transmitted value tells us exactly what slice of the bits the controller is reading. |

### 5. Clean up

Once you're done testing, remove the test row:

```sql
DELETE FROM QrPool WHERE Code = '10000001';
```

## If the test passes

Reply back to the assistant — we'll then:
1. Change `_configRangeStart` from `50001001` to `10000001` in `QrPoolService.cs`
2. Add a one-time migration that flushes the old over-24-bit pool entries
   (so they don't keep failing) and regenerates the pool in the new range
3. Re-upload the new pool to the device on next sync

## If the test fails

Send the assistant the WatchEvent line from the Real-Time Monitor showing
what the device actually read for `10000001`. The exact value (and whether
it's `0`, a partial number, or unrelated) will narrow down the WG34 bit
layout the device is using.
