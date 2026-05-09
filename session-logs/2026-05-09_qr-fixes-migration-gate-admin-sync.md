# Session Log — 2026-05-09 (Evening session)

**Session start:** 2026-05-09 (afternoon)
**Session end:** 2026-05-09 (late evening)
**Branch:** feature/pwa-cloud
**Follows:** [2026-05-09_crash-forensics-and-screenshots.md](2026-05-09_crash-forensics-and-screenshots.md)
**Goal:** Diagnose and fix QR daily pass scan flow on real hardware, then close the migration-defaults loop, then audit all Admin Panel ↔ main app data flows so admin edits actually surface in the main app.

---

## Part 1 — QR daily pass: theory falsified, real fix found

### Symptom
Customer scanned the QR for pass code `50001695`. Real-time monitor showed `Card = 2`, "Card Not Found (Not Registered)" — door denied.

### What yesterday's commit (`fbd0210`) had assumed
The Hikvision/Dnake controller is configured with QR card-number format "8H10D". The previous fix author read this as "8 Hex characters → 10 Decimal digit card number" and added hex encoding: pool code `50001695` → QR text `02FAF59F` (8-char uppercase hex). The device was supposed to convert `0x02FAF59F = 50001695` and look up the card. It didn't work.

### What the live testing actually showed
Three controlled tests in sequence:

| QR text content | Device reports card | What this tells us |
|---|---|---|
| `02FAF59F` (hex of 50001695) | `2` | Reads digits from the start, **stops at the first letter** (`F`), strips leading zero |
| `00989681` (hex of 10000001) | `989681` | All digits, no letters — strips leading zeros, reports remaining digits |
| `10000001` (plain decimal) | `10000001` + door OPENS | Full match; pool entry was already uploaded with `addUnSortCard("10000001")` |

The device is NOT doing hex→decimal conversion. It just reads the numeric digits in the QR text and uses them as the card number. "8H10D" is a vendor-internal format label that doesn't mean what the previous commit assumed.

### Fix
Reverted `QrCodeDisplayDialog.GenerateQrCode` to embed the pass code verbatim in the QR — no encoding. The existing pool range (50001001+) works fine. No DB migration, no pool regeneration.

### Bonus fix in the Monitor
Same scan still showed "Card Not Found (Not Registered)" because `MonitorViewModel` only looked up `AccessCard` records and used a hardcoded `evt.CardNumber.StartsWith("5000")` heuristic to decide whether something was a QR code. Replaced with a real `IQrPassService.ValidateAndUseAsync` lookup so:

- Visitor name from the QrPass record is shown instead of generic "QR Guest"
- Status reads "QR Pass (1/2)" with live use count, not "Not Registered"
- Pool range can change in the future without breaking detection

Bilingual labels added: `DispQrPass`, `DispQrPassExpired`, `DispQrPassUsedUp`, `DispQrGuest`.

### Diagnostic scripts kept in-repo
[tools/qr-test/](../tools/qr-test/) — `insert_test_pool_code.sql`, `insert_test_qrpass.sql`, `README.md` — the SQL + workflow that pinned the device behaviour. Future device-format puzzles can use the same test pattern.

### Commit
`786fa2e` — "QR daily pass: revert hex encoding + recognise pool codes in Monitor"

---

## Part 2 — Migration-defaults gate

### Background
`MigrationService` already does the right thing on import (only copies essential fields, sets sentinels for the rest: `Phone="MIG-N"`, `SubscriptionType="Migrated"`, numeric fields = 0). But the placeholders just sit there forever — staff don't notice them when they renew a subscription on a migrated player.

### What changed
Two complementary nudges:

**Edit Player dialog: red-border highlight on default-value fields.** When a migrated player is opened, fields that still hold sentinel values get a soft red 2px border (`#E5 73 73`). Border clears the moment the user types a real value into the field. Highlighted fields:

- Phone (matches `MIG-\d+`)
- Subscription (selected item == "Migrated")
- Fee + Paid (still empty/zero)
- Height + Weight (still empty/zero)

The numeric-zero fields are only flagged while Phone or Subscription is still a sentinel — once the record is no longer "migrated", zero is treated as a legitimate value and we stop pestering the user.

**Renew Subscription command: blocked while sentinels remain.** Bilingual warning explains what to update, button opens the Edit dialog (which shows the red borders). After the user saves with a normal edit-reason audit, the record is re-fetched from the DB and re-checked. If clean, renewal proceeds; otherwise a second warning appears and renewal aborts. The renewal command itself naturally fixes 3 of the 5 sentinels (SubscriptionType / SubscriptionFee / AmountPaid all get overwritten by the renew flow), so the gate only enforces Phone and SubscriptionType — Height/Weight aren't blocked because 0 is legitimate for non-migrated players.

### Bilingual messages added
- `MigratedRenewBlockedTitle` / "تحديث البيانات مطلوب قبل التجديد"
- `MigratedRenewBlockedMessage` (parameterised with current Phone + SubscriptionType for clarity)
- `MigratedRenewStillBlocked` (shown if the Edit dialog was saved but Phone/Subscription are still placeholders)

### Commit
`df2e160` — "Migration-defaults gate: red-border highlight in Edit + block Renew until cleaned"

---

## Part 3 — Admin Panel ↔ Main app data-flow audit

User reported that subscription plans configured in the Admin Panel (حديد, رشاقة) never appeared in the main app's Add Player dialog. That triggered a full audit of every Admin section.

### Findings

| Admin section | Admin writes to | Main app reads from | Status |
|---|---|---|---|
| Income Categories | LookupItems (Category=`IncomeCategory`) | LookupItems (same) | ✅ matched |
| Expense Categories | LookupItems (Category=`ExpenseCategory`) | LookupItems (same) | ✅ matched |
| Product Categories | LookupItems (Category=`ProductCategory`) | LookupItems (same) | ✅ matched |
| **Subscription Plans (dedicated page)** | **`SubscriptionPlans` table** | **LookupItems (Category=`SubscriptionPlan`)** | ❌ **mismatch — fixed** |
| Subscription Plans (legacy entry on Categories page) | LookupItems (Category=`SubscriptionPlan`) | (now nothing reads it) | ⚠️ orphan — removed |
| Products | Products via `IProductRepository` | Products via `IProductRepository` | ✅ matched |
| Suppliers | Suppliers via `ISupplierService` | (admin-only, no main-app reader) | ✅ N/A |
| Time Groups | TimeGroups via `ITimeGroupService` | TimeGroups via same service | ✅ matched |
| Users & Permissions | Users via `IAuthService` | Users via same service | ✅ matched |
| QR Pool | QrPool via `IQrPoolService` | QrPool via same service | ✅ matched |
| App settings (gym name, logo, …) | AppSettings via `IAppSettingsService` | AppSettings via same service | ✅ matched |

### Subscription Plans fix (the one disconnect)

Wired the main app's player dialogs to the same table the Admin Panel writes to:

1. `AppDbContext` now exposes `DbSet<SubscriptionPlan>` + new `SubscriptionPlanConfiguration` maps it to the existing `SubscriptionPlans` table (no SQL migration — Admin already creates/uses it).
2. `ILookupRepository.GetActiveSubscriptionPlansAsync()` returns active rows ordered by SortOrder; `LookupRepository` implements via EF.
3. `ILookupService` passes through, so dialogs already injecting `ILookupService` get the new method with no DI plumbing changes.
4. `AddEmployeeDialog` + `RenewSubscriptionDialog`: `LoadPlansFromDbAsync` now calls `GetActiveSubscriptionPlansAsync`. Mapping `Domain.Entities.SubscriptionPlan` → the local WPF `SubscriptionPlan`:
   - `Type` = `NameEn` (the form uses NameEn as the SubscriptionType key)
   - `DisplayName` = `NameAr` in Arabic UI, `NameEn` otherwise
   - `MonthlyRate` = Price normalised to per-month for the dialog's `Fee = MonthlyRate × months` calc:
     - Days → `Price × 30 / Duration`
     - Months → `Price / Duration`
     - Unlimited → `Price` (flat)

### Categories page cleanup

The "Subscription Plans" entry in the Categories admin page was removed from the dropdown. Saving plans there used to write to LookupItems but nothing reads from there anymore — leaving the option in the UI would have lured admins into adding plans in the wrong place.

### Commit
`a44f560` — "Wire main app subscription plans to the SubscriptionPlans table the Admin actually writes to"

---

## Build & deploy

### Clean rebuild
- `dotnet clean` + delete all `bin/obj` + `dotnet restore -r win-x86`
- Sequential `dotnet publish` for the 3 customer apps (parallel publish hits file-lock contention on the shared Application/Infrastructure obj folders)

### Output
| App | Folder | Size | Built |
|---|---|---|---|
| Main app | `publish/customer-deploy/main-app/` | 194.2 MB | 2026-05-09 22:15 |
| Admin panel | `publish/customer-deploy/admin-panel/` | 195.5 MB | 2026-05-09 22:15 |
| POS terminal | `publish/customer-deploy/pos-terminal/` | 195.2 MB | 2026-05-09 22:16 |

All self-contained x86 builds. Customer needs no .NET runtime.

---

## Files touched (this session)

### QR fix commit (`786fa2e`)
- MODIFIED: `src/AccessControlPro.WPF/Views/QrCodeDisplayDialog.xaml.cs` — reverted hex encoding
- MODIFIED: `src/AccessControlPro.WPF/ViewModels/MonitorViewModel.cs` — IQrPassService injection + ValidateAndUseAsync fallback
- MODIFIED: `src/AccessControlPro.WPF/Resources/Strings.resx` + `Strings.ar.resx` — DispQrPass / DispQrPassExpired / DispQrPassUsedUp / DispQrGuest
- MODIFIED: `src/AccessControlPro.WPF/Helpers/LanguageManager.cs` — wired the new strings
- NEW: `tools/qr-test/insert_test_pool_code.sql`, `insert_test_qrpass.sql`, `README.md`

### Migration-gate commit (`df2e160`)
- MODIFIED: `src/AccessControlPro.WPF/Views/AddEmployeeDialog.xaml.cs` — `ApplyMigratedDefaultsHighlighting` + `SetMigratedHighlight`
- MODIFIED: `src/AccessControlPro.WPF/ViewModels/EmployeesViewModel.cs` — `EnsureMigrationDefaultsResolvedAsync` gate at top of `RenewSubscriptionAsync`
- MODIFIED: `src/AccessControlPro.WPF/Resources/Strings.resx` + `Strings.ar.resx` — `MigratedRenewBlockedTitle`/`Message`/`StillBlocked`
- MODIFIED: `src/AccessControlPro.WPF/Helpers/LanguageManager.cs` — wired the new strings

### Admin↔Main sync commit (`a44f560`)
- MODIFIED: `src/AccessControlPro.Infrastructure/Persistence/AppDbContext.cs` — `DbSet<SubscriptionPlan>`
- NEW: `src/AccessControlPro.Infrastructure/Persistence/Configurations/SubscriptionPlanConfiguration.cs`
- MODIFIED: `src/AccessControlPro.Domain/Interfaces/ILookupRepository.cs` — `GetActiveSubscriptionPlansAsync`
- MODIFIED: `src/AccessControlPro.Infrastructure/Persistence/Repositories/LookupRepository.cs` — EF implementation
- MODIFIED: `src/AccessControlPro.Application/Interfaces/ILookupService.cs` — pass-through
- MODIFIED: `src/AccessControlPro.Application/Services/LookupService.cs` — pass-through
- MODIFIED: `src/AccessControlPro.WPF/Views/AddEmployeeDialog.xaml.cs` — switch source + `NormalizeToMonthlyRate`
- MODIFIED: `src/AccessControlPro.WPF/Views/RenewSubscriptionDialog.xaml.cs` — same switch
- MODIFIED: `src/AccessControlPro.Admin/ViewModels/CategoriesViewModel.cs` — removed orphan entry

---

## Lessons noted

- **"8H10D" was a vendor format label, not a description of behaviour.** When fixing device interop, always test live before assuming the manual's wording corresponds to actual firmware behaviour. A 30-second test (one QR text → read what the device reports) saved hours of theorising.
- **Two different storage paths for the "same" thing is the real bug, not whichever path is wrong.** The Admin and main app each had a perfectly reasonable place to store subscription plans. The disconnect was that nobody noticed they were different. The audit pattern (every admin section: where does it write? where does the consumer read?) caught it in minutes once asked systematically.
- **Sentinels survive better than flags.** The migration sets sentinel values (`MIG-N`, `"Migrated"`, zeros) instead of an `IsMigrated` boolean. That meant we could detect "still needs cleanup" by inspecting actual data, with no schema change, and the detection naturally fades as the user fills the fields in.
- **Parallel `dotnet publish` to separate output folders still contends on shared `obj/`** — sequential is the safe default. Already noted yesterday but bit us again.

---

## Outstanding / deferred

- **`appsettings.json` exclusion fix** — same issue still bites every deploy (dev defaults overwrite production config on the customer PC and the VPS). One-line `<CopyToPublishDirectory>Never</CopyToPublishDirectory>` per csproj would close it permanently.
- **SDK init per card** — wrapper still re-initialises `FCardCDrive` for every single `addUnSortCard`. ~500 ms wasted per card.
- **Garbage RFID reads filtering** — card numbers like `5242882`, `1476395007` from antenna noise; filter by sane upper bound.
- **First-install "KILLED OR CRASHED" false positive** — SetupWizard closes without OnExit, so the LastRunStateTracker always flags first reboot as a crash. Cosmetic.
- **Row 786 numeric overflow** in the customer's local DB still causes a single error per cloud sync (now reported correctly as `errors=1` instead of the old `errors=10694` cascade). Identify and fix that one row whenever convenient.
