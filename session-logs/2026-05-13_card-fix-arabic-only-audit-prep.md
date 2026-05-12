# Session Log — 2026-05-13 (evening)

**Session start:** 2026-05-13 (late evening)
**Branch:** feature/pwa-cloud
**Follows:** [2026-05-09_qr-fixes-migration-gate-admin-sync.md](2026-05-09_qr-fixes-migration-gate-admin-sync.md)
**Goal:** Diagnose customer-reported issues with the cloud site, fix the card-format
mismatch that was making Event records show "Not Registered" for cards that ARE
registered, switch the website to Arabic-only + Light & Clean theme, and prepare a
comprehensive forensic audit script the user can run against the live cloud DB.

---

## Customer complaints that drove this session

User listed five concerns:

1. **Arabic toggle on the website not working** — despite the `@rendermode InteractiveServer` fix on 2026-05-09.
2. **Some records empty in cloud, full in local** — suggesting a sync gap.
3. **Design too complex** — too many fields, mixed Arabic/English/numbers, too much info per page.
4. **Need an API** — for future iOS app and other integrations.
5. **Local↔cloud time not syncing** — timezone issue.

User also asked: is there a more modern/stable framework than Blazor? After honest
discussion (no real customers yet, only testing) we agreed the pragmatic path is to
fix the actual problems in the existing stack before considering a Next.js rewrite
that the customer-reported problems would NOT solve (most are backend/UX issues,
not framework issues). One future-positive: extracting a clean REST API from Blazor
would unlock the iOS-app path without a full rewrite.

---

## Part 1 — Card-format mismatch bug (the real find)

### Symptom
Cloud Events page showed `Player = "-"` and `Details = @NotRegistered` for cards
that clearly belong to registered players in the WPF main app.

### Customer-supplied evidence
Two screenshots, two card numbers:

| Player.CardNo | Event.CardNumber | Math |
|---|---|---|
| `0366549` | `3665490` | `3665490 / 10 = 366549` ✓ |
| `0374205` | `3742052` | `3742052 / 10 = 374205` ✓ |

Pattern confirmed. The card reader emits the value in 8H10D Wiegand format (with
trailing parity/check digit and no leading zero); the manually-registered player
record has the value with a leading zero and no trailing digit. The two strings
look different to `==` even though they represent the same physical card.

This is the same family of bug as the QR-pool 8H10D issue we hit on 2026-05-09 —
which was the trigger for the user noticing this one too.

### Why an orphan AccessCard exists at all
When the device scans a card the WPF code calls `IAccessCardRepository.GetByCardNumberAsync(scannedNumber)`.
The previous implementation only tried two variants:
- Exact match.
- Variant with leading-zero stripped.

Neither matched `3665490` against `0366549`. The event was saved without an
EmployeeId reference, and an AccessCard row got created later (presumably during a
re-sync) with the scanned format — leaving the cloud with two AccessCard rows for
the same physical card.

### Fix (commit `36340ac`)

**Two sides:**

1. **`AccessCardRepository.GetByCardNumberAsync` (WPF + cloud sync side)** — added a
   third fallback that converts the card number to a `long`, divides by 10, and
   tries the result with and without a leading zero. Numeric-only guard
   (`cardNumber.All(char.IsDigit)`) prevents touching QR pool codes like
   `QR05040944441143`. Future scans will link straight to the registered AccessCard.

2. **`OwnerEvents.razor` SQL JOIN (cloud Events display)** — added a second
   `LEFT JOIN "Players" p2 ON p."Id" IS NULL AND p2."CardNo"::bigint = c."CardNumber"::bigint / 10`
   that only fires when the primary join missed AND both card numbers are pure
   digits. Historical events now display the correct player name without changing
   any data.

Future cleanup option (not done): one-time SQL migration to merge the orphan
AccessCard rows into the registered ones. Will revisit after the user runs the
audit script and we see how widespread the orphan-card situation is.

---

## Part 2 — Website goes Arabic-only + Light & Clean theme (commit `184704a`)

### User decision
Removing English entirely makes sense — customer base is Iraqi/Arabic. Bilingual
support added complexity without value and the toggle button was a frequent source
of bugs. Style switched from dark teal to bright/airy per user's "Light & Clean"
choice in the question UI.

### Implementation — Arabic-only

Minimal-touch approach so we don't have to sweep through every `@L("en","ar")`
call across ~17 Razor pages:

- `SessionState.Language = "ar"` (was `"en"`)
- `SessionState.IsArabic` now hardcoded `true`
- `SessionState.Dir` hardcoded `"rtl"`
- `SessionState.ToggleLanguage()` is now a no-op (kept so existing handlers still compile)
- Toggle button removed from `LandingLayout.razor` (top nav + mobile bar)
- Toggle button removed from `MainLayout.razor` (sidebar)

The dozens of existing `@L("English", "Arabic")` helper calls now always resolve
to the Arabic branch. A future cleanup pass can flatten them but it's purely
cosmetic — runtime behaviour is already Arabic-only.

### Implementation — Light & Clean CSS

Single-source-of-truth approach: changed the `:root` CSS variables in `app.css`.
The variables cascade to most components.

| Variable | Old (dark) | New (light) |
|---|---|---|
| `--bg-primary` | `#081B1D` | `#F5F7FA` |
| `--bg-card` | `#0E2E30` | `#FFFFFF` |
| `--bg-card-hover` | `#123A3D` | `#EEF2F5` |
| `--text-primary` | `#FFFFFF` | `#1A2E2F` |
| `--text-secondary` | `#A0B4B5` | `#5F7374` |
| `--border` | `#1A4A4D` | `#D9E1E5` |
| `--accent` | `#44A1A0` | `#44A1A0` (kept) |
| `--accent-hover` | `#5BB8B7` | `#38898A` |

The sidebar deliberately stays dark — added a separate variable set
(`--sidebar-bg`, `--sidebar-text`, `--sidebar-text-muted`) and updated the
`.sidebar` / `.nav-link*` rules to use those. This gives strong contrast against
the new light main content and preserves the visual identity.

### Known visual followup needed

~11 hardcoded dark backgrounds and ~44 hardcoded `color: white`
declarations elsewhere in `app.css` were NOT touched. They may now produce
white-on-white text or isolated dark cards on individual pages (modals, tables,
form sections). The right way to fix these is iteratively, with screenshots from
the user showing what looks wrong, rather than guessing and risking breaking
working areas. User will deploy + screenshot the issues for a second-pass fix.

---

## Part 3 — Cloud DB forensic audit prep

User asked for a broad investigation of "all data in cloud" to surface bugs and
logical errors like the card mismatch.

### Approach
Wrote a comprehensive read-only PostgreSQL audit script — 25+ individual checks
grouped into 8 sections — that the user can run on the VPS and send back the
output for analysis. **No DML, completely safe to run on production.**

### Sections
1. **Cards & Players** — orphan cards, recoverable orphans (matchable via /10 rule), duplicate cards, players without/with-multiple cards.
2. **Access Events** — totals, unmatched (CardId=NULL), future/ancient dates, orphan door references, age distribution.
3. **Players / Subscriptions** — total count, still-Migrated leftovers, invalid date ranges, expired-but-active, zero fees, overpaid, unknown subscription types.
4. **Finance / Transactions** — uncategorised, zero/negative amounts, future-dated.
5. **QR Pool** — status distribution, assigned-without-timestamp, invalid validity window, stale-assigned.
6. **Cloud Sync Health** — attempts by status (last 7d), recent failures, gap detection in last 30 days.
7. **Audit Logs** — totals, most-frequent actions, anonymous actions.
8. **Referential Integrity** — dangling card→employee refs, dangling txn→employee refs, lost tombstones.

### Files
- `tools/db-audit/audit.sql` — the full script.
- `tools/db-audit/README.md` — how to run, what each section checks, expected follow-up actions.

### Expected follow-ups (after the user runs it)
- **Cleanup migration** for orphan AccessCards that map to a registered player via /10 rule.
- **Backfill migration** for players still flagged as `Migrated` (force-update via Edit dialog).
- **Hotfix** for any logical bug discovered (e.g., future-dated events → timezone confirmation).
- **Cloud sync gap** investigation if section 6c shows long gaps.

---

## Files touched this session

### Card fix commit (`36340ac`)
- MODIFIED: `src/AccessControlPro.Infrastructure/Persistence/Repositories/AccessCardRepository.cs` — `/10` fallback in `GetByCardNumberAsync`.
- MODIFIED: `src/AccessControlPro.Web/Components/Pages/OwnerEvents.razor` — second LEFT JOIN to Players for orphan-card display recovery.

### Arabic-only + Light & Clean commit (`184704a`)
- MODIFIED: `src/AccessControlPro.Web/Services/SessionState.cs` — language hardcoded to Arabic.
- MODIFIED: `src/AccessControlPro.Web/Components/Layout/LandingLayout.razor` — language toggle buttons removed.
- MODIFIED: `src/AccessControlPro.Web/Components/Layout/MainLayout.razor` — sidebar language toggle removed.
- MODIFIED: `src/AccessControlPro.Web/wwwroot/app.css` — `:root` variables flipped to light theme; sidebar variables added.

### Audit script commit (this commit)
- NEW: `tools/db-audit/audit.sql`
- NEW: `tools/db-audit/README.md`
- NEW: `session-logs/2026-05-13_card-fix-arabic-only-audit-prep.md` (this file)

### Build artifacts
- `publish/web-deploy/` — 106.2 MB, includes card fix + Arabic-only + Light theme
- `publish/customer-deploy/main-app/` — 194.2 MB, includes card fix
- `publish/customer-deploy/admin-panel/` — 195.5 MB
- `publish/customer-deploy/pos-terminal/` — 195.2 MB

---

## What the user should do next

1. **Deploy the web build** to VPS (backup `appsettings.json` first, upload, restore config, restart).
2. **Deploy the new WPF main-app** to the customer PC (backup `appsettings.json`, copy files, restore config).
3. **Verify the card-fix worked** — open Events page on cloud, the two example cards (`3665490`, `3742052`) should now show their player names instead of `-`.
4. **Screenshot any pages where Light & Clean broke layout** (white-on-white text, dark cards that escaped the theme switch). Send for iterative fix.
5. **Run the audit script** on the VPS (`psql gymcloud_drag -f audit.sql > audit_results.txt`). Paste the entire output back — we'll analyse and decide what to fix.

---

## Lessons noted

- **8H10D format issue is the recurring villain.** Hit the same root cause now for the third time (QR codes 2026-05-09, then physical-card lookup today). Worth eventually moving to a `NormalizeCardNumber()` helper used uniformly at every entry point so we don't keep adding /10 fallbacks one place at a time.
- **Don't sweep CSS blind.** Tempting to find-and-replace `color: white` → `color: var(--text-primary)` but that would break the sidebar which IS still dark. Iterative screenshot-driven fixes are slower but safer.
- **The user's "more stable framework" question often masks specific problems.** Walking through their actual symptoms (sync, Arabic, design, time, API) showed only ONE genuinely framework-driven concern (API for iOS), and even that can be solved without a rewrite. Worth pushing back gently on rewrite-as-a-cure-all.

---

## Deferred items (still on the list)

From this and previous sessions, items waiting for a quiet moment:

- **`appsettings.json` `<CopyToPublishDirectory>Never</CopyToPublishDirectory>`** on all 4 csproj — one line each, closes the recurring "dev config overwrites production" deploy footgun.
- **SDK `Initialize`/`Shutdown` instead of process-restart on Stop Monitor** — eliminates the ~3-second blackout every time the user uses the monitor.
- **RFID garbage-read filter** — reject impossibly-large card numbers from antenna noise (saw `5242882`, `1476395007` in logs).
- **Setup-wizard "KILLED OR CRASHED" false positive** — wizard close path doesn't fire OnExit so first reboot after install always flags as a crash.
- **Customer's row 786 numeric overflow** — single bad Player record that still errors during sync. Find and fix the bad value.
- **`@L(en, ar)` flatten** — purely cosmetic cleanup, replace 100s of helper calls with the Arabic string directly. Wait until the dust settles on other work.
- **Light & Clean second pass** — fix the hardcoded color leftovers from today's first pass.
- **Cloud DB cleanup migration** — once audit is reviewed, merge orphan AccessCards / backfill Migrated players.
