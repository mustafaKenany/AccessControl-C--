# Migrating from Other Systems

How to bring a new customer's existing player/card data into AccessControlPro.

> **Note**: An automated CSV importer is on the roadmap but not yet built. Until it ships, follow the SQL-based procedure below.

## What data the customer typically wants to bring

Customers come from systems like:
- Excel spreadsheets with member lists
- Older gym software (FCard, ZK, Hikvision native tools, custom Access DBs)
- Handwritten paper records (yes, this still happens)

The data they want to migrate, in priority order:
1. **Player names** (full name, phone) — without this they have to retype everyone
2. **Card numbers** — so physical cards keep working at the door
3. **Subscription end dates** — so the gym knows who's currently active
4. **Player photos** — nice to have, often skipped

Audit logs, freeze history, and old transactions are almost never worth migrating.

## Pre-migration: understand the source data

Before importing anything, get a CSV from the customer with at minimum:

```csv
FullName,Phone,CardNumber,SubscriptionEndDate
احمد محمد,07712345678,0366549,2026-12-31
علي حسن,07712345679,0366550,2026-06-30
...
```

Validate:
- [ ] No duplicate `CardNumber` values
- [ ] No empty `FullName` values
- [ ] Card numbers are all digits (no spaces, dashes, letters)
- [ ] Dates are in a consistent format

If their data is messy (which it usually is), spend an hour cleaning the CSV in Excel before importing. Garbage in, garbage out.

## The "migration sentinels" pattern

AccessControlPro has a built-in pattern for partial imports: any field we don't know gets a **sentinel value** that signals "this needs to be filled in":
- Unknown phone → `MIG-{N}` where N is the player's row number
- Unknown subscription type → `Migrated`

These sentinels do two things:
1. Mark the row as imported (vs. created in the app)
2. Block renewals on the player until a human re-enters the missing data

This is good UX: the migration succeeds even with imperfect data, AND the customer is forced to validate each player before they can renew them — so within a few months the dataset cleans itself.

## SQL-based import procedure

For now (until the CSV importer ships), do the import via SQL:

### Step 1: Stage the data
On the customer's PC:

```sql
CREATE TABLE #MigrationStaging (
    FullName        NVARCHAR(200),
    Phone           NVARCHAR(50),
    CardNumber      NVARCHAR(50),
    EndDate         DATE,
    Notes           NVARCHAR(500)
);

-- Use SSMS Import Wizard or BULK INSERT to fill from CSV
BULK INSERT #MigrationStaging
FROM 'C:\migration\source.csv'
WITH (FIRSTROW = 2, FIELDTERMINATOR = ',', ROWTERMINATOR = '\n', CODEPAGE = '65001');
```

### Step 2: Insert into Employees with sentinels for missing fields

```sql
INSERT INTO Employees (
    FullNameEn, FullNameAr, Phone, CardNo,
    SubscriptionType, StartDate, EndDate,
    SubscriptionFee, AmountPaid,
    CreatedAt, UpdatedAt
)
SELECT
    s.FullName,
    s.FullName,                              -- Same in both lang fields; the renewal gate forces re-entry
    COALESCE(NULLIF(s.Phone, ''), 'MIG-' + CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS VARCHAR)),
    s.CardNumber,
    'Migrated',                              -- Sentinel — renewal forces real plan
    s.EndDate,                               -- Their original end date
    s.EndDate,
    0, 0,
    GETUTCDATE(), GETUTCDATE()
FROM #MigrationStaging s
WHERE NOT EXISTS (SELECT 1 FROM Employees e WHERE e.CardNo = s.CardNumber);
```

### Step 3: Insert AccessCards rows so the device knows about them

```sql
INSERT INTO AccessCards (
    EmployeeId, CardNumber, IsActive,
    ValidFrom, ValidTo, EffectiveTimes,
    CreatedAt, UpdatedAt
)
SELECT
    e.Id, e.CardNo, 1,
    GETUTCDATE(), e.EndDate, 65535,
    GETUTCDATE(), GETUTCDATE()
FROM Employees e
WHERE e.SubscriptionType = 'Migrated'
  AND NOT EXISTS (SELECT 1 FROM AccessCards c WHERE c.CardNumber = e.CardNo);
```

### Step 4: Push the cards to the device

This must happen via the WPF app (not SQL) because the SDK call needs to run from a Windows process with the native libraries loaded.

In the WPF app:
- Open **Devices** page
- Select your device
- Click "Sync All Cards to Device" (or similar — exact label depends on the build)
- Wait for completion

This pushes every `AccessCards.IsActive=1` row to the device's internal card list.

## After import

The customer will see all their migrated players in the Players page, all with:
- "Migrated" as their subscription plan
- "MIG-N" as the phone if it was missing

When the receptionist tries to **Renew** any of them, the app shows:
- "This player has incomplete information. Please update Phone and Subscription Type first."
- The Edit dialog opens; receptionist fills in real values; renewal proceeds

Within a few months of normal use, every migrated player has been touched at least once and the `Migrated` sentinel count drops to zero. You can watch this progress on `/superadmin/data-quality` per gym.

## What NOT to migrate

| Don't migrate | Why |
|---|---|
| Old transactions | Accounting records — moving them across systems creates audit confusion. Start fresh. |
| Old audit logs | No value, big table |
| Old access events | Privacy + size — let the new system build a fresh history |
| Player photos | Usually live in a separate folder on the old system; copy by hand only if customer asks |
| Freeze history | Almost always stale; let the customer re-freeze whoever's currently frozen |

## Customer expectations

Tell the customer up front:
- "We'll migrate names + cards + subscription end dates. Everything else (transactions, history, photos) we'll start fresh — that's actually cleaner."
- "Every imported player will need a one-time data check the first time you renew them. The app guides you through it."
- "Expect ~50% of your imported players to come back and renew within 2 months — the others have probably stopped coming anyway."

## When to skip migration entirely

For very small gyms (< 100 active members), or if the source data is garbage (handwritten ledgers, no phone numbers), it's often faster to just **re-enroll everyone fresh** as they come in over the next month. Gives you cleaner data and lets the customer learn the app.
