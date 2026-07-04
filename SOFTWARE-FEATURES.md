# Gym Access-Control & Management System — Complete Feature Specification

> **Purpose of this document.** This is a complete, behavior-focused specification of every feature in an existing gym access-control + management product (desktop apps + cloud portal + PWA). It is written to be handed to an AI agent or dev team so they can **replicate the exact same features in a second product that runs on different access-control hardware.** It describes *what the software does and the business rules*, not the current source code. Wherever a behavior is tied to the specific door-controller hardware, it is called out under **[HARDWARE-SPECIFIC]** so you know what to re-implement against your own controller SDK.
>
> No credentials, keys, IP addresses, or customer data are included here by design.

---

## 0. Product Overview

A bilingual (English / Arabic, full RTL) system that lets a gym control who can open its gates, manage memberships and money, and gives the gym owner a cloud dashboard + a member phone app.

It has **five deployable parts**:

| Part | Tech (reference impl.) | Role |
|---|---|---|
| **Main desktop app** | .NET WPF, local SQL Server | The operator's control center: members, cards, gates, events, finance |
| **Admin desktop app** | .NET WPF | Back-office: users/roles, subscription plans, suppliers, purchase orders, inventory, reports, backup |
| **POS desktop app** | .NET WPF | Cashier: product sales, card top-ups, member debt, shifts, receipts |
| **Cloud portal** | Blazor Server, PostgreSQL, multi-tenant | Gym-owner web dashboard + vendor SuperAdmin fleet management |
| **Member PWA** | Blazor + service worker | Installable phone app: member sees subscription, visits, entry QR, gets renewal push |

Architecture is **Clean Architecture**: Domain (entities/rules) → Application (services/interfaces/DTOs) → Infrastructure (DB, hardware SDK wrapper) → UI apps. Replicating on new hardware should only require swapping the **hardware SDK wrapper** (Section 6) and re-mapping a few enums; everything else is portable.

---

## 1. Core Domain Entities

Replicate these entities and their relationships:

- **Member (a.k.a. Player / Employee):** bilingual name (En/Ar), phone, photo, DOB/gender/ID (optional), subscription type, `StartDate`, `EndDate`, `SubscriptionFee`, `Discount`, `AmountPaid`, `MaxVisits`, `UsedVisits`, `IsFrozen`, freeze dates, `CardNo`, sync status. Has many **AccessCards** and many **AccessEvents**.
- **AccessCard:** `CardNumber`, door permissions, effective-times (visit count), valid-from/valid-to, time-schedule index, per-device sync flags. Belongs to a Member.
- **Device (controller):** name, model, IP, MAC, port, serial number. Has many **Doors**.
- **Door:** `DoorNumber` (1–4), name, status, lock state, working schedule (start/end time, 24h flag, working days). Belongs to a Device.
- **AccessEvent:** timestamp (UTC), card number, member (resolved), door, device, event type/code.
- **SubscriptionPlan:** bilingual name, duration + duration-type (Days/Months/Unlimited), price, max visits, effective-times, active flag, sort order.
- **Transaction (finance):** date, type (Income/Expense), category, amount, description, payment method, status, reference.
- **User (operator):** username, password hash, display name, role, permission list, active flag.
- **AuditLog:** timestamp, user, action, entity type/id, details (En + Ar), status.
- **DeletedRecord (soft delete):** archived entity + who/when/why.
- **FreezeHistory:** member, start, end, reason.
- **QrPoolCode:** numeric code, status, source (Local/Cloud), guest name/phone, max-uses, used-count, valid-to, door permissions.
- **TimeGroup (schedule):** bilingual name, hardware schedule index, weekly 7-day schedule (up to 3 segments/day).
- **POS entities:** Product, Category, Supplier, PurchaseOrder + items, StockMovement, PosShift, sale/receipt records.
- **AppSettings:** gym name, logo, owner/company info, feature flags.

---

## 2. Member (Player) Management — Desktop

The central screen. Shows a virtualized, paged list (handles thousands of members smoothly).

**List & search**
- Columns: photo, name (En + Ar), card no, sync-status dot, subscription type, fee, paid, remaining, card count, actions.
- Search box matches **name, phone, or card number**. Card-number search is digit-aware: it does an exact/prefix lookup, tolerant of a leading zero (e.g. `0366549` and `366549` match the same card).
- Search is debounced (~300 ms).
- **Performance rule [IMPORTANT]:** list/grid screens that don't display the photo must **not** load the photo blob per row — project only the columns shown. Load the full member (with photo) on demand only when editing / printing / viewing profile. This is the single biggest perf win.
- **Filter pills:** All, Expiring, Renewed, Frozen, Expired, Active. Each filter loads **one page at a time** from the database (never the whole set).
- For Expiring/Renewed, a **period** selector: Today / This Week / Last Week / This Month / Last Month.
- Header badges: total count, with-card count, without-card count (computed over the whole filtered set, not just the page).

**Per-member actions**
- **Add** member (dialog below).
- **Edit** (reloads full record so the photo isn't blanked).
- **View profile** (tabs: overview, subscriptions, cards, transactions/visits).
- **Assign card** / **Remove card**.
- **Freeze** (pick a reason) / **Unfreeze**.
- **Renew subscription**.
- **View cards** (all cards ever issued to the member).
- **Delete** (soft delete with a required reason).

**Add / Edit member dialog**
- Fields: name En, name Ar, phone, (optional) email/DOB/gender/ID/address, subscription type (from plans), start date (defaults today), end date (auto-computed from plan), fee (auto-filled from plan), **discount (defaults to `0`)**, amount paid, notes, photo (upload/change/remove).
- Live "remaining = fee − discount − paid" display.
- Validation: name En required, phone required, subscription type required, no duplicate card number.
- **Registration flow [shipped behavior]:** after saving a new member, if a card number was entered, the app **auto-opens the Assign-Card dialog** (pre-filled, to push the card to the gate) and **then** offers to print the registration receipt. If no card number was entered, it skips straight to the receipt. The Assign-Card step stays cancellable. This shortcut exists for less-technical operators.

**Bulk operations dialog:** choose Freeze / Unfreeze / Extend (N days) / Upload-all-to-device; choose a target set (All active / Expiring in 7 days / Expired / Frozen); runs in batches with a progress bar.

**Create-missing-cards:** one click generates AccessCard records for every active member that has a card number but no card record (so a later device sync pushes them). Members with no number are skipped and reported.

**Print list:** printable members report.

---

## 3. Subscription Plans & Pricing

- Plans are defined in the Admin app: bilingual name, **duration + duration-type (Days / Months / Unlimited)**, price, max visits, effective-times, active, sort order.
- **Length & fee computation:**
  - Days-type plan → `EndDate = StartDate + (Duration × periods)` days; fee = `Price × periods` (flat).
  - Months-type plan → `EndDate = StartDate.AddMonths(Duration × periods)`; fee = `Price × periods`.
  - This matters: a "20-day month" plan must give exactly 20 days and one flat charge — never round to a calendar month.
- **Discount** is an absolute amount (not %). **Outstanding balance = Fee − Discount − AmountPaid.** Partial payment is allowed; the unpaid remainder is tracked and surfaced in Finance.
- **Max visits:** `0` = unlimited (date-based only); `>0` = capped number of entries. Hardware ceiling is 65535.
- Multiple daily-pass tiers are supported by simply creating 2+ plans named as daily tiers; the daily-pass dialog then shows a tier selector and books revenue per tier.

---

## 4. Membership Lifecycle & States

State is **computed from data**, not stored:

- **Active:** `EndDate ≥ today` AND not frozen AND (if visit-limited) `UsedVisits < MaxVisits`.
- **Expiring soon:** active but `EndDate` within the next N days (default 7).
- **Expired:** `EndDate < today` (or visits exhausted).
- **Frozen:** `IsFrozen = true` regardless of dates (time is paused).
- **Renewed:** recently pushed through the renewal flow.

**Freeze mechanics:** stores a reason in freeze history; on unfreeze, the frozen duration is added back to `EndDate`; the visit counter is **not** reset. While frozen, the member is removed/disabled on the gate and not re-synced.

**Renewal:** sets `StartDate = today`, `EndDate = today + duration`, resets `UsedVisits = 0`, clears frozen, then re-pushes all active cards to the gate with the new validity. Fee/discount/paid can change at renewal.

**Soft delete:** moves the member to an archive table with reason + who/when; best-effort removes their cards from the gate. A "Deleted Records" screen lists archives (no restore flow in the reference product — if you add restore, keep the photo).

---

## 5. Doors & Schedules

- A device has **1–4 physical doors**. Each door has a `DoorNumber` (1–4) and an operator-chosen **name** (e.g. "Entry", "Exit", "Weights Room").
- **Per-door working schedule:** 24-hour flag, or start/end time, plus working days (Mon–Sun).
- **Door actions:** rename, set schedule, view who has access, **test-open** (pulse the relay), delete.
- **Door-permissions UI rule [shipped behavior]:** the card-assignment and renewal dialogs build their door checkboxes **from the actual configured doors** — showing each door's real name and only as many doors as the hardware has (2-door controller → 2 checkboxes, 4-door → 4). Do **not** hardcode "Door 1–4". Fall back to a generic 4-door list only if no doors are configured yet.

**Time Groups (access schedules):** named weekly schedules (up to 3 time segments per day, per day on/off), mapped to a hardware schedule index and pushed to the controller. One can be marked default.

---

## 6. Access Cards & Gate Sync  **[HARDWARE-SPECIFIC — the part to re-implement]**

This is the core hardware integration. Everything else is portable; this section is where a different controller changes things.

### 6.1 What a card carries to the controller
- **Card number** (string, usually numeric).
- **Door permissions** — an **8-character string = 4 doors × 2 chars**, where each door is `"01"` (allowed) or `"00"` (denied). Position 1 → door 1 … position 4 → door 4. Example: `"01010000"` = doors 1 and 2 allowed, doors 3–4 denied. A door the hardware doesn't have, or an unchecked door, contributes `"00"`. *(Re-map this format to your controller's door bitmap.)*
- **Effective-times / visit count:** `MaxVisits` if visit-limited (controller decrements per swipe); `65535` for unlimited.
- **Valid-from / Valid-to (permit time):** the validity window the controller enforces.
- **Time-schedule index** (which on-device weekly schedule applies) and a holiday flag.
- **Open mode** (Ordinary is the default; the SDK also exposes first-card / always-open / patrol / anti-theft modes).

### 6.2 Expiry enforcement — the single most important rule
- **The gate controller is the ONLY expiry enforcer**, via each card's **Valid-To**. Valid-To is set to **`EndDate` at 23:59:59** (end of the member's last day). **Never** use a far-future fallback (a past bug set validity ~10 years out, letting expired members open the door).
- A background "expiry monitor" that removes cards every few seconds is **deliberately disabled** — doing per-few-second SDK removals froze the controllers. Rely on Valid-To instead.
- **Expired AND frozen members are never synced to the gate.** They stay in the local DB only. All sync paths filter to `ValidTo.Date ≥ today` AND not-frozen.
- A per-device **"Revoke expired on gate"** maintenance action re-pushes every expired card with a past Valid-To so the controller rejects any card that still carries stale far-future validity. Operator runs it once per gym after upgrading.

### 6.3 Sync operations
- **Assign card:** writes/overwrites the card on selected devices with the fields above. Keying is by **card number**, so re-writing the same number **updates** the card (this is why renewals work) — but note it **resets the on-device visit counter**.
- **Renew:** re-pushes active cards with the new Valid-To and reset visits.
- **Remove / disable:** re-push the card with a **past Valid-To** so the controller rejects it on next swipe (preferred over hard delete).
- **Bulk / repopulate device:** used when a controller is swapped or first set up. Two-phase: (1) push all active, non-frozen, non-expired members first so the gym is usable within seconds; (2) push the full QR pool in the background. Cards are processed in **batches (~30 per SDK session)** with connect→push→cleanup between batches to avoid native memory bloat. Progress is reported ("Loading members 40/120", then "Loading QR 1240/4566").
- **Multi-device:** operations are serialized (a lock prevents concurrent native SDK calls); each device is pinged first; a failed device is recorded and skipped, no auto-retry.

### 6.4 Swipe handling & events
- The controller stores swipe records; the app **fetches** them and stores them as AccessEvents. (Note: in the reference product, events are only pulled while the live Monitor is running — a background periodic fetch is a recommended improvement.)
- On a valid entry the server increments `UsedVisits`; when `UsedVisits ≥ MaxVisits` the card is disabled on the next sync.
- Event codes distinguish card-open, repeat/re-swipe, expired, invalid, button-open, remote open/close, door sensor, alarm, and system events. **[HARDWARE-SPECIFIC]** map your controller's codes to these categories; the reference SDK required swapping a couple of code pairs to normalize them.

### 6.5 Hardware SDK surface to replace
Provide an adapter exposing: initialize/shutdown, **search network** (UDP broadcast discovery), initialize/configure a device, read/calibrate device clock, update device IP, **add/overwrite card**, delete card, get device info, **fetch stored records**, set opening-hours schedule, anti-passback, remote open/close door, set door-open delay, start/stop live monitoring, alarm control. Keep **clock calibration** — Valid-To enforcement depends on the controller's clock matching server time.

---

## 7. QR / Daily-Pass / Visitor System

Optional (feature-flagged per gym).

- **QR pool:** a block of pre-generated **numeric** codes (act like virtual cards) pushed to the controller so it recognizes them. Each code has status (available/assigned/used/expired), validity window, max-uses, and door permissions. **Collision guard:** if a pool code equals a real member card number, release it (the member card wins).
- **Daily pass:** issued on demand to a walk-in (name optional). Pulls a pool code, charges the fee to income, valid until end of day, limited uses. No member record is created (anonymous). Supports multiple price tiers (Section 3).
- **Temporary physical card:** hand a guest a temp card for the day; track outstanding temp cards; "return card" expires it on the gate.
- **Manual check-in:** operator admits a guest and just records the fee (no card).
- **[HARDWARE-SPECIFIC] reader divisor quirk:** some readers divide/scale the scanned value; those gyms need a configured divisor and a QR range placed below member-card numbers so encoded visitor codes decode correctly. Expose a divisor setting and validate the encode/decode round-trip against your reader.

---

## 8. Events / Access Logs / Live Monitor

- **Events screen:** virtualized table of gate events — time (displayed in local +3), member name, card no, event type, device, door. Filters: event type, device, period (Today … This Year), and text search by name/card. "Show all" loads on demand.
- **Live Monitor window:** start/stop a real-time feed of the last ~20 events, color-coded allowed/denied; optional full-screen "projector" display for a reception TV with running stats.

---

## 9. Finance & Cash Flow — Desktop

- **Finance screen:** summary cards (total revenue, total expenses, net profit, outstanding). Transactions table with income (green) / expense (red), category, status; period filter + search. Outstanding is computed as a DB SUM of `Fee − Discount − AmountPaid` over unpaid members (with a capped display list for performance). Print income / print expenses.
- **Cash Flow screen:** add income / add expense with category, date, amount, description, method; filter by category/type/period.
- Revenue breaks down by subscription type; upcoming-renewal view.

---

## 10. POS (Cashier) App

Feature-flagged on per gym.

- **Product catalog** as touch tiles, grouped by category; out-of-stock tiles disabled; live stock count shown.
- **Barcode scanning** (hardware wedge or manual) adds to cart; "not found" message on miss.
- **Cart:** line items with qty ± and remove, per-item discount, order-level discount (amount + reason), live subtotal/total.
- **Member lookup** by name/card shows card balance + current debt.
- **Payment methods:** cash; **pay from card balance** (validates sufficient stored balance); **on credit** (adds to the member's debt, settled later).
- **Card top-up:** add funds to a member's stored card balance.
- **Collect debt:** take a payment against a member's outstanding debt.
- **Receipts:** 80 mm thermal format with gym header; print or save; income/expense receipts too.
- **Shifts:** open/close shift; sales blocked unless a shift is open; end-of-shift summary.
- **Daily summary:** totals by count/amount/items and by payment method; printable.

---

## 11. Admin (Back-Office) App

- **Dashboard:** members total, expiring-this-week, frozen, monthly revenue/expenses/net, unpaid balances, supplier debt, low-stock count; top-10 expiring; low-stock list; recent audit entries; **backup-health warning** (no backup in 3+ days or 3+ consecutive failures).
- **Users & roles:** create/edit/deactivate operators; username immutable; reset password; role (Admin/User) + **granular permission set** (see Section 17). Never hard-delete a user with history — deactivate.
- **Products / Categories / Suppliers:** CRUD; categories cover income, expense, and product groupings (with optional rate).
- **Purchase orders & inventory:** create PO (supplier, items, qty, unit cost, discount, amount-paid), which records stock-in; PO history with partial-payment tracking (Pending/Partial/Paid, remaining balance); stock-movements ledger (In/Out with reference/supplier/user); supplier-balances (owed) view; low-stock monitoring.
- **Reports:** members/expiry, finance, inventory snapshot, sales-by-product (with profit = revenue − cost), purchases, stock movements, supplier history. Print + CSV export; all period-based reports have date-range pickers.
- **Alerts:** expiring-soon / already-expired / frozen tabs.
- **Audit log viewer:** immutable, searchable, paginated.
- **Backup & restore:** manual native DB backup to a chosen file; restore with a strong confirmation (replaces the DB; single-user during restore; auto-recover to multi-user; rollback on failure); backup health tracked.
- **Settings:** company/gym/owner/contact info + logo (used on receipts and headers).
- **Subscription plans** and **Time groups** management (Sections 3 and 5).
- **QR pool management:** pool status counts, low-pool "needs regeneration" warning, assign a code to a guest, deactivate a code, cleanup expired.

---

## 12. Reminders (WhatsApp)

- Lists members expiring within a configurable window (plus already-expired).
- One-click **WhatsApp** per member via a `wa.me` deep link that opens WhatsApp Desktop/Web with a **pre-filled message** — **no paid gateway.**
- Editable, persisted message template with placeholders `{name} {gym} {date} {days}`.
- Local `07xx` numbers auto-formatted to international `9647xx` (Iraq). Adapt the country logic for other locales.

---

## 13. Cloud Owner Portal (Web)

Multi-tenant: each gym is reached at its own subdomain; the subdomain resolves to that gym's isolated database.

- **Owner login:** username/password; brute-force lockout (e.g. 5 tries / 15 min per IP); server-side session cookie (HttpOnly/Secure/SameSite=Lax) bound to IP + user-agent; ~7-day session; change-password.
- **Dashboard:** stat cards — total / active / expiring / expired / frozen members, today's entries, monthly revenue, outstanding. **Sync-status bar** showing last sync time from the gym PC + status, a **"Force full sync"** button, and expandable error details. Recent events list; expiring-soon list (days-left color-coded). All times shown at local +3.
- **Players:** paged tiles with status ring, search by name/phone/card, filter by status + expiry window; **member profile** with tabs: subscription, payments, renewals, freezes, cards, visits. WhatsApp-share the member's portal login.
- **Finance:** revenue / expenses / net / outstanding by period; transaction list; per-member outstanding; simple aging.
- **Events:** synced gate events with period filter + search; QR/visitor codes badged.
- **QR guest pass:** create a pass (guest name/phone, fixed 2-use / 24 h), render the QR (300×300), WhatsApp-share or download PNG; list active passes with deactivate.
- **Users:** manage portal staff (create/edit/deactivate, role).
- **Devices:** read-only list (name, serial, IP, doors).
- **Backup status & Audit logs:** synced sync-history and action log, searchable, period-filtered.

---

## 14. Member PWA (Phone App)

- **Installable** (web manifest: standalone display, icons, portrait, start at the member dashboard). Add-to-home-screen on iOS/Android.
- **Member login:** phone number + last 4 digits of card; longer session (~30 days) so the installed app rarely re-prompts.
- **Member dashboard:** membership card (photo/avatar, name En+Ar, status badge, subscription type, **days remaining**, visits used/max with progress bar). Tabs: overview, my card (with entry **QR**), payments, visits history.
- **Push notifications (Web Push / VAPID):** opt-in; a scheduled server job (≈twice daily) scans for members expiring within N days and sends a **renewal reminder** push that deep-links to the member dashboard. Degrades gracefully to no-op if push keys aren't configured.
- **Offline:** service worker caches static assets, network-first for data.
- Fully responsive + dark theme.

---

## 15. Cloud Sync (Desktop ⇄ Cloud)

- **One sync endpoint** the desktop posts to, authenticated by a **per-gym API key** header; payload is gzip-compressed JSON; large-payload + zip-bomb-safe limits; the whole apply is atomic (all-or-rollback), returning `{ success, rows, errors[] }` and logging a sync record.
- **Modes:** **Full** (clear each table then insert — for first setup / reset) and **Delta** (upsert changed rows + apply delete tombstones — for routine syncs).
- **Entities synced (superset):** Members, AccessEvents, Devices, Doors, Transactions, Users, AuditLogs, DeletedMembers (tombstones), AppSettings, AccessCards, QrPool, SubscriptionPlans, POS shifts, FreezeHistories, Products, TimeGroups.
- **Post-sync fleet metrics:** the master record for the gym is updated with last-sync time, player counts, active count, members-without-card, last-event time — feeding the SuperAdmin health board.
- **Control channel:** the desktop polls for `{ forceFullSync, locked, lockMessage, onlineEnabled, posEnabled, qrPoolEnabled }`. `forceFullSync` is read-and-clear (fires once). Feature flags/lock are also available via a lightweight read-only poll (~every 5 min).
- **Pull endpoints:** the desktop can pull cloud-managed **users** and cloud-assigned **QR codes**.
- **Balance rule:** `Discount` syncs to cloud; the portal balance is `Fee − Discount − Paid` (keep the columns in every per-gym schema).

---

## 16. SuperAdmin / Fleet Management (Vendor)

Separate vendor login (own brute-force lockout, ~7-day session).

- **Fleet dashboard:** total/active/expired gyms, total members, monthly revenue, syncs in last 24 h; recent sync logs; gym overview.
- **Gyms:** list with status/expiry; **add gym** (name, owner contact, unique DNS-safe subdomain auto-slugged with collision increment, expiry default +1 yr, QR opt-in); WhatsApp renewal-reminder generator for gyms expiring within 30 days.
- **Gym details:** info (subdomain, DB name, API key, expiry, QR config); **remote lock** (blocks that gym's desktop within ~5 min with a custom message; auto-locks if the gym can't reach cloud for 7+ days; data untouched); **feature toggles** vendor-only: Online, POS, QR pool; QR-pool config (pool size, code range, local/cloud split, monthly fee, reader divisor); live gym stats; **impersonate owner** (support view).
- **Gym health board:** per-gym vitals (players, active, no-card count, last event, last sync, last diagnostics, app version, recent crashes) with green/yellow/red scoring and thresholds.
- **System health:** DB size, connections, table sizes, global totals.
- **Revenue analytics:** monthly + yearly projection, per-gym breakdown (subscription + QR fee), overdue/paying counts.
- **Diagnostics:** list/download/delete uploaded diagnostic bundles per gym; bulk-delete >30 days.
- **Provisioning:** a setup-time endpoint (guarded by a provisioning secret) that **auto-creates a new tenant**: slugified unique subdomain + a fresh per-gym database with full schema + a master registry row; idempotent by API key; rolls back the registry row if DB creation fails.

---

## 17. Cross-Cutting Systems

**Localization (En/Ar):**
- User-facing entity strings are stored bilingually (NameEn/NameAr); audit details stored in both languages.
- Desktop uses resource strings with an English default + an Arabic satellite; **any new string must exist in both**, and the Arabic satellite must ship with the build (a stale satellite silently shows English).
- Runtime language toggle flips all labels and **flow direction (LTR/RTL)**; Arabic uses an appropriate font.

**Timezone:**
- **Store all timestamps in UTC; display at +3 (Iraq, no DST).** Subscription date comparisons use the local date. Keep the controller's clock calibrated to server time so Valid-To enforcement agrees.

**Permissions & security:**
- Role + **granular permission keys** per user (comma-separated list checked at every screen/action). Categories include: app access (Main/POS/Admin/Cloud), dashboard, members (view/add/edit/delete/assign-card/remove-card/reassign/freeze/renew/reports/sync), devices, doors (view/open-close/settings), events, finance, cash-flow, POS (sales/shift/discount/print/summary), time-groups, plans, logs, deleted-records, monitor, data-migration, QR passes, backup, admin (manage users/settings). **Admin = all.**
- Passwords hashed (PBKDF2/BCrypt). License file (below) and sensitive local data encrypted. Portal sessions bound to IP + user-agent; API endpoints require the per-gym key; parameterized queries throughout; per-gym DB isolation.

**Licensing / license-lock (desktop):**
- Machine-bound license (hardware fingerprint), signed key with an expiry, validated on activation and each startup. States: active / expired / not-activated / hardware-changed / clock-tamper / invalid.
- **Anti-rollback:** a monotonic "peak date" is kept so setting the clock backward is detected. **Offline behavior:** an emergency unlock code (valid for the current + previous month) grants a short grace window; a multi-day grace covers cloud outages so a paying gym is never hard-locked by a transient network problem.

**Auto-update:**
- App polls a **version manifest** (`{ version, downloadUrl, sha256, sizeBytes, releaseNotes_en/ar, mandatory, minVersion }`). If `mandatory` or `current < minVersion`, force it (OK-only prompt); otherwise offer update / snooze / skip.
- Download → **verify SHA-256** → pre-update backup → write a pending-update sentinel → launch a **separate updater helper** that waits for the app to exit, swaps files (defers still-locked files to reboot), and relaunches. The running updater is the *old* one, so updater fixes protect the *next* update.

**Diagnostics bundle:**
- On demand, on a periodic interval, or after a crash, the app zips: user note, system/app/gym info, sanitized settings, network test, DB snapshot (member counts by status, sync stats), recent event tail, and its own logs — and uploads it (API-key auth, with trigger + version headers). Vendor reviews it instead of remote-desktop sessions. Retention: keep the newest N per gym.

**Rolling logs:**
- Separate rotating text logs (operations/session, QR, SDK, card-disable, DB errors, import, diagnostics) with ~60-day retention, launch + day separators, timestamped bilingual entries, legacy-format migration.

**Database migrations:**
- On startup, run an **idempotent** schema updater (`IF NOT EXISTS` guards for columns/indexes/tables/triggers). All apps call it; SQL serializes the DDL. **Never gate migrations behind a "version marker"** that can skip them — every new column must reach old installs (a marker once caused "Invalid column name" errors on older gyms). Cloud schema changes apply on service startup the same idempotent way.

**Setup wizard (new install):**
- Steps: welcome → database connection (test) → gym info + logo → create admin account → summary/finish (+ desktop shortcuts). Auto-generates a unique cloud API key and seeds default subscription plans. For **reinstalling an existing gym**, leave the cloud-key blank (recovered from the kept DB) and do **not** re-run provisioning (that would create a new tenant).

---

## 18. UI / UX Conventions (reference)

- Dark theme, teal accent; card-based layouts; status dots/badges (green=active/online, red=expired/offline, orange/teal=warning/frozen).
- Virtualized long lists; debounced search; deferred/paged loading; confirmation dialogs on destructive actions; inline validation with clear messages; toast notifications; progress bars for long operations (sync, bulk, migration).
- Prominent search box on list screens; filters as pills/chips; period pickers on all time-based views.
- Printing: 80 mm thermal for receipts/ID cards; standard paper for list/finance reports; CSV export for data.

---

## 19. Replication Checklist (for the second product)

1. **Port the domain model + rules** (Sections 1–5, 9–17) as-is — they are hardware-independent.
2. **Rewrite only the hardware adapter** (Section 6.5) for your controller; preserve: Valid-To expiry enforcement, "never sync expired/frozen," 23:59:59 end-of-day permit time, batch sync, clock calibration.
3. **Re-map** the door-permission encoding (Section 6.1) and the event-code categories (Section 6.4) to your controller.
4. Keep the **8-char/4-door** semantics at the UI/business layer even if your hardware differs — or generalize it to your door count, but keep the "build door checkboxes from configured doors" behavior.
5. Preserve the **money rules** (`balance = fee − discount − paid`, discount default 0, plan duration math).
6. Preserve **UTC-store / +3-display**, bilingual strings + RTL, idempotent migrations, mandatory auto-update with SHA verification, and the diagnostics/health telemetry.
7. If cloud is in scope, replicate the **multi-tenant** model (subdomain → per-gym DB), the **single sync endpoint** (full/delta) with the entity set in Section 15, and the SuperAdmin fleet controls (lock, feature flags, provisioning, health).

---

## 20. Database Schema

Two databases. The **desktop app uses local Microsoft SQL Server**; the **cloud uses PostgreSQL** (a master registry DB + one isolated DB per gym). The cloud per-gym schema is a **synced subset** of the local schema plus a few cloud-only tables. All money columns are `decimal(18,2)`; all timestamps are UTC. Recreate whatever storage you like — these are the fields the features depend on.

### 20.1 Local database — Microsoft SQL Server (desktop) — 24 tables

Schema is created/updated at startup by an **idempotent migrator** (`IF NOT EXISTS` guards). Recovery model = SIMPLE. Several mutable tables carry `CreatedAt`/`UpdatedAt` with an `AFTER UPDATE` trigger that stamps `UpdatedAt`. `Employees` has a `RowVersion` (optimistic concurrency).

**Employees** (members/players) — PK `Id` identity
| Column | Type | Notes |
|---|---|---|
| FullNameEn / FullNameAr | nvarchar(200) | En required; Ar optional |
| CardNo | nvarchar(50) | unique filtered index where `<>''` |
| SubscriptionType | nvarchar(100) | plan name |
| Phone | nvarchar(30) | unique filtered index where `<>''` |
| PhotoData | varbinary(max) | photo blob (**never load in list queries**) |
| Height / Weight | float | |
| SubscriptionFee / Discount / AmountPaid | decimal(18,2) | balance = Fee − Discount − Paid |
| StartDate / EndDate | datetime2 | UTC; EndDate drives expiry |
| Notes | nvarchar(1000) | |
| IsFrozen | bit | filtered index on =1 |
| FreezeStartDate | datetime2 | |
| CardBalance | decimal(18,2) | prepaid POS balance, CHECK ≥ 0 |
| Debt | decimal(18,2) | POS credit owed (separate from CardBalance) |
| MaxVisits / UsedVisits | int | 0 = unlimited |
| RowVersion | rowversion | concurrency |
| CreatedAt / UpdatedAt | datetime2 | trigger-stamped |

Indexes: CardNo, Phone, IsFrozen, StartDate, EndDate, FullNameEn, FullNameAr, SubscriptionType, Outstanding `(Fee,Paid) WHERE Fee>Paid`, UpdatedAt.

**AccessCards** — PK `Id`; FK `EmployeeId → Employees(Id)` ON DELETE CASCADE
| Column | Type | Notes |
|---|---|---|
| CardNumber | nvarchar(20) | unique |
| CardPassword | nvarchar(10) | PIN, usually empty |
| CardType | nvarchar(30) | default "Standard" |
| OpenMode | int | 0=Ordinary,1=FirstCard,2=AlwaysOpen,3=Patrol,4=AntiTheft |
| DoorPermissions | nvarchar(20) | **8 chars = 4 doors × "01"/"00"** |
| EffectiveTimes | int | **visit count**: 0=immediate-reject, 1–1000=countdown, 65535=unlimited |
| TimePeriodIndex | int | schedule index 1–64 |
| HolidayEnabled | bit | |
| IsActive / IsSyncedToDevice | bit | |
| ValidFrom / ValidTo | datetime2 | **ValidTo = the gate-enforced expiry** |
| CreatedAt / UpdatedAt | datetime2 | trigger-stamped |

**Devices** — PK `Id`; unique `SerialNumber`. Columns: Name, IP, MAC, SerialNumber, TCPPort (8000), UDPPort (8101), Password, Gateway, SubnetMask, DeviceType, IsOnline, CreatedAt. *(Port/password/gateway are **[HARDWARE-SPECIFIC]**.)*

**Doors** — PK `Id`; FK `DeviceId → Devices(Id)` CASCADE. Columns: DoorNumber (1–4), Name, Status, IsLocked, WorkStartTime (time), WorkEndTime (time), Is24Hours (bit), WorkingDays (nvarchar, "1..7" Mon–Sun), CreatedAt.

**AccessEvents** — PK `Id`; FK `DoorId → Doors(Id)`, FK `CardId → AccessCards(Id)` ON DELETE SET NULL. Columns: EventType (int RecordType), EventCode (int), Timestamp (UTC), Details. Indexes on (DoorId,Timestamp), CardId, EventType, Timestamp DESC.

**Users** (operators) — PK `Id`. Columns: Username, PasswordHash, DisplayName, Role (default "User"), IsActive, Permissions (comma-separated keys), CreatedAt/UpdatedAt.

**AuditLogs** — PK `Id`. Columns: Action, EntityType, EntityId, Details, DetailsAr, PerformedBy, Timestamp. Indexes on Action, Timestamp DESC.

**DeletedEmployees** (soft-delete archive) — snapshot of the member (OriginalId, names, CardNo, SubscriptionType, Phone, PhotoData, Height, Weight, Fee, Paid, dates, Notes) + DeleteReason, DeletedBy, DeletedAt, OriginalCreatedAt.

**FreezeHistories** — PK `Id`; FK `EmployeeId` CASCADE. Columns: FreezeStart, FreezeEnd (null=ongoing), FreezeDays, Reason, CreatedAt/UpdatedAt.

**SubscriptionPlans** — PK `Id`. Columns: NameEn, NameAr, Duration (int), DurationType ("Days"/"Months"/"Unlimited"), Price (CHECK ≥ 0), MaxVisits, EffectiveTimes (65535), IsActive, SortOrder, CreatedAt/UpdatedAt. Auto-seeds a few defaults if empty.

**TimeGroups** — PK `Id`; unique `HardwareIndex` (1–64). Columns: NameEn, NameAr, HardwareIndex, IsDefault, ScheduleJson (weekly `{"Mon":["09:00-13:00",...]}`, `{}` = 24/7), CreatedAt/UpdatedAt. Seeds a "24/7 Full Access" default.

**Transactions** (finance ledger) — PK `Id`; FK `RelatedEmployeeId → Employees(Id)` SET NULL. Columns: Type (int Income/Expense), Category, Amount (CHECK ≥ 0), Description, PaymentMethod (int), CreatedBy, CreatedAt, DiscountAmount, DiscountReason, Reference (e.g. "PO-12"). Rich indexing for reporting.

**Products** — PK `Id`; unique `Barcode` where `<>''`. Columns: Name, NameAr, Barcode, Price (CHECK ≥ 0), CostPrice, Category, Stock (CHECK ≥ 0), IsActive, CreatedAt/UpdatedAt.

**Suppliers** — PK `Id`. Columns: Name, Phone, Address, ContactPerson, IsActive, CreatedAt.

**PurchaseOrders** — PK `Id`; FK `SupplierId`. Columns: OrderDate, TotalAmount, Discount, AmountPaid, PaymentStatus ("Unpaid"/"Partial"/"Paid"), Notes, CreatedBy, CreatedAt.

**PurchaseOrderItems** — PK `Id`; FK `PurchaseOrderId` CASCADE, FK `ProductId`. Columns: Quantity, UnitCost.

**StockMovements** — PK `Id`; FK `ProductId` CASCADE, FK `PurchaseOrderId` (opt), FK `TransactionId` SET NULL (opt). Columns: Type (int Purchase/Sale/Adjustment), Quantity, UnitPrice, Reference, Description, CreatedBy, CreatedAt.

**PosShifts** — PK `Id`. Columns: OpenedBy, OpenedAt, ClosedAt, OpeningCash, ClosingCash, TotalSales, TotalCashSales, TotalCardSales, Variance, Status ("Open"/"Closed"), UpdatedAt.

**QrPasses** (legacy single passes) — PK `Id`; unique `PassCode`. Columns: PassCode, PlayerName, Phone, ValidFrom, ValidTo, MaxUses, UsedCount, Fee, IsActive, CreatedBy, CreatedAt, DeviceId, DoorNumber, DeviceName.

**QrPool** (pre-generated pool) — PK `Id`; unique `Code`. Columns: Code (varchar 20), Status (0 avail/1 assigned/2 used/3 expired), Source ("Local"/"Cloud"), GuestName, GuestPhone, Reason, AssignedAt, UsedAt, ExpiredAt, MaxUses (2), UsedCount, ValidFrom, ValidTo, DoorPermissions ("01010000"), CreatedAt/UpdatedAt, IsUploadedToDevice.

**CardDeviceSyncs** (per-card-per-device sync state) — PK `Id`; unique (`AccessCardId`,`DeviceId`); FKs CASCADE. Columns: IsSynced, SyncedAt, LastError.

**AppSettings** (single row Id=1) — CompanyName, GymName, LogoPath, DevLogoPath, Phone, Address, Owner, CloudApiKey (mirrored so a reinstall recovers the cloud key from the DB).

**LookupItems** (config dropdowns) — PK `Id`. Columns: Category ("IncomeCategory"/"ExpenseCategory"/"ProductCategory"/"SubscriptionPlan"), Name, NameAr, NumericValue (e.g. monthly rate), SortOrder, IsActive. Auto-seeds bilingual income/expense/product categories.

**MonitorLocks** (single live monitor across PCs) — MachineName, UserName, AcquiredAt, HeartbeatAt (10 s heartbeat; > 30 s stale = abandoned).

### 20.2 Cloud database — PostgreSQL (multi-tenant)

Column semantics mirror the local schema above (same meanings; PostgreSQL types: `SERIAL`, `VARCHAR`, `TIMESTAMP`/`TIMESTAMPTZ`, `DECIMAL(18,2)`, `BOOLEAN`, `TEXT`, `INT`). Fast substring search on members uses the **`pg_trgm`** extension (GIN trigram indexes on name/phone/card). *(Note: `EffectiveTimes` is the visit-count, and `DoorPermissions` is the same 8-char/4-door string as local — not a time bitmap.)*

**MASTER database (one instance — fleet registry):**

- **Gyms** — one row per tenant. Key columns: `Id`, `Name`, `Subdomain` (DNS label, unique), `ApiKey` (per-gym sync auth), `DatabaseName` (`gymcloud_<subdomain>`), `IsActive`, `ExpiresAt`, `CreatedAt`, owner (`OwnerName/Phone/Email`), `SubscriptionPrice`, `Notes`, **fleet metrics** (`LastSyncAt`, `PlayerCount`, `ActivePlayerCount`, `MembersWithoutCard`, `LastEventAt`), **control flags** (`ForceFullSync`, `IsLocked`, `LockMessage`, `OnlineEnabled`, `PosEnabled`), **QR config** (`QrPoolEnabled`, `QrPoolSize`, `QrRangeStart`, `QrMonthlyFee`, `QrReaderDivisor`).
- **Sessions** — server-side session store (the browser cookie holds only an opaque token). Columns: `Token` (PK), `Role` (SuperAdmin/Owner/Admin/User/Player), `DisplayName`, `UserId`, `GymDatabase`, `GymId`, `CreatedAt`, `ExpiresAt` (owner ~short, player ~90 d), `RevokedAt`, `LastSeenAt`, `LastIp`, `LastUserAgent`. Indexed on `ExpiresAt` and (`UserId`,`GymId`).
- **DiagnosticsUploads** — metadata for uploaded diagnostic ZIPs: `Id`, `GymId`, `FileName`, `FilePath`, `FileSizeBytes`, `AppVersion`, `Trigger` ('manual'/'crash'), `UploadedAt`. Keep newest N per gym.

**PER-GYM tenant database (`gymcloud_<subdomain>`):** created fresh per gym with the full schema. Tables **populated by the desktop sync**: `Players`, `AccessCards`, `Devices`, `Doors`, `AccessEvents`, `Users`, `Transactions`, `AuditLogs`, `DeletedEmployees` (delete tombstones), `AppSettings`, `QrPool`, `SubscriptionPlans`, `PosShifts`, `FreezeHistories`, `Products`, `TimeGroups`. Tables **created cloud-side**: `CloudSyncLogs` (SyncType, Status, Details, SyncedAt) and `PushSubscriptions` (`PlayerId`, `Endpoint` unique, `P256dh`, `Auth`, `CreatedAt`, `LastSentAt` — for Web Push). These per-gym tables are lean versions of the local tables (e.g. cloud `Players` omits the photo blob but keeps names/phone/dates/fee/discount/paid/visits/frozen; `AccessCards` keeps CardNumber/valid dates/EffectiveTimes).

### 20.3 Sync payload & upsert

The desktop posts a single gzip JSON payload with these arrays: `players, accessEvents, devices, doors, transactions, users, auditLogs, deletedEmployees, appSettings, accessCards, qrPool, subscriptionPlans, posShifts, freezeHistories, products, timeGroups`. **Full** mode clears each table then inserts; **Delta** mode does `INSERT … ON CONFLICT (Id) DO UPDATE` (idempotent upsert) + applies `deletedEmployees` tombstones. After apply, the master `Gyms` row's fleet metrics are refreshed.

---

## 21. Domain, Multi-Tenancy, SSL & VPS Deployment

This is the exact method used, so the second product can copy it. **Substitute your own domain/server/email/keys** — the sensitive values (passwords, DB credentials, API keys) are intentionally not in this document.

**Reference topology (our production):** domain `hmtech.solutions` (registrar Hostinger) on a single Hostinger VPS (Ubuntu; public IP published via DNS). Nginx terminates TLS and reverse-proxies to a Kestrel/Blazor process; PostgreSQL runs on the same box. App lives at `/var/www/gymapp/` (the Blazor executable + `appsettings.json` + `wwwroot/` sibling, **not** nested), run as a systemd service `gymapp`.

### 21.1 The one-record wildcard model (why it scales to any new gym)
Adding a new gym touches **only the database** — no DNS, nginx, or certificate change — because three wildcards line up:
1. **DNS:** a single wildcard `A` record `*.<domain> → <VPS_IP>` (plus apex + `www`). Any subdomain resolves instantly.
2. **TLS:** one **wildcard Let's Encrypt certificate** for `<domain>` + `*.<domain>` covers every subdomain.
3. **Nginx:** one server block for `server_name <domain> *.<domain>` proxies everything to the app.
So `newgym.<domain>` works the moment a `Gyms` row with `Subdomain='newgym'` exists.

### 21.2 Subdomain → tenant resolution (request pipeline)
```
Browser (https://<sub>.<domain>)
  → Nginx :443 (TLS terminate; sets X-Forwarded-Proto/For, Host)
  → Kestrel 127.0.0.1:5000
  → UseForwardedHeaders()           (so Request.IsHttps/Host are correct)
  → SubdomainMiddleware             (extract <sub> from Host)
       • reserved {www, admin, api, ""} → root/superadmin, not a tenant
       • SELECT * FROM "Gyms" WHERE LOWER("Subdomain")=LOWER(@s) AND "IsActive"
       • store {Id, Name, DatabaseName, ApiKey} in HttpContext.Items["CurrentGym"]
  → Session middleware              (validate opaque cookie token vs Sessions table, IP/UA bound)
  → Blazor component                (opens the tenant DB gymcloud_<sub> for all queries)
```
Tenant isolation is at the **database** level (separate PG database per gym) — no row-level filtering, no cross-tenant leakage, per-gym backup/restore.

### 21.3 New-gym provisioning (`POST /api/provision-gym`)
Called by the desktop setup wizard (guarded by a provisioning secret), or from SuperAdmin. Logic: slugify the requested subdomain or gym name (`lowercase`, strip non-alphanumeric; empty → `"gym"`); reject reserved words; ensure uniqueness by incrementing (`basmia → basmia1 → basmia2`); `CREATE DATABASE gymcloud_<sub>` and build its schema; insert the master `Gyms` row **last** (rollback drops the DB on failure). **Idempotent by API key** — re-running with the same key returns the existing subdomain, never a duplicate.

### 21.4 Nginx (essentials)
- `:80` server for `<domain> *.<domain>` → `301` to HTTPS.
- `:443 ssl http2` server for `<domain> *.<domain>`:
  - `ssl_certificate /etc/letsencrypt/live/<domain>/fullchain.pem;` + `privkey.pem`.
  - Forward headers: `Host $host`, `X-Real-IP`, `X-Forwarded-For`, `X-Forwarded-Proto $scheme`.
  - **WebSocket upgrade** for Blazor Server SignalR: `proxy_http_version 1.1; Upgrade $http_upgrade; Connection "upgrade";`
  - `location / { proxy_pass http://127.0.0.1:5000; }`
- App side (`Program.cs`): `UseUrls("http://0.0.0.0:5000")`, raise Kestrel `MaxRequestBodySize` (~50 MB for sync), `UseForwardedHeaders()` **before** the subdomain/session middleware.

### 21.5 Wildcard TLS (Let's Encrypt, DNS-01)
Wildcards require the DNS-01 challenge:
```bash
certbot certonly --manual --preferred-challenges dns \
  --email <your-email> --agree-tos --no-eff-email \
  -d "<domain>" -d "*.<domain>"
```
Add the shown `_acme-challenge` TXT record at the registrar, wait ~2 min, confirm. Cert lands in `/etc/letsencrypt/live/<domain>/`. New subdomains need **no** new cert. Renew (~every 90 days) with the same command; set a reminder ~60 days ahead (DNS-01 wildcard renewal is manual unless you add a registrar DNS-API plugin).

### 21.6 Deploying the web app
```bash
cp /var/www/gymapp/appsettings.json /tmp/appsettings.backup.json   # preserve secrets
systemctl stop gymapp
scp <publish-output>/* root@<VPS_IP>:/var/www/gymapp/              # upload new build
cp /tmp/appsettings.backup.json /var/www/gymapp/appsettings.json    # restore secrets
systemctl start gymapp
```
Schema ALTERs run idempotently on startup. Verify: `systemctl status gymapp`, then `curl https://<domain>/health` → `{"status":"ok"}` (the health endpoint just pings the master DB). A one-time `COMException`/old-process-dying message on restart is harmless.

### 21.7 Release hosting on the same domain
The desktop auto-update manifest and ZIPs are served as **static files** from `wwwroot/releases/`:
- `GET /releases/latest.json` (static) and `GET /api/version/latest` (app endpoint, 5-min cache) both return the manifest `{ version, downloadUrl, sha256, sizeBytes, releaseNotes_en/ar, mandatory, minVersion, releasedAt }`.
- Publish = upload `AccessControlPro-v<ver>.zip` + `latest.json` to `wwwroot/releases/`, then **verify the server-side SHA-256 matches local** before the manifest goes live (a flaky upload once shipped a corrupt zip).

### 21.8 Replication checklist (domain/infra)
1. Register a domain; add a **wildcard A record** `*.<domain> → <server IP>` (+ apex + www).
2. Issue a **wildcard TLS cert** (Let's Encrypt DNS-01).
3. Nginx: one HTTP→HTTPS redirect + one HTTPS block for `<domain> *.<domain>` with forwarded headers + WebSocket upgrade → `127.0.0.1:<port>`.
4. App: Kestrel on localhost; `UseForwardedHeaders()` → **subdomain middleware** (reserved words + `Gyms` lookup) → **session middleware** → tenant DB per request.
5. PostgreSQL: master DB (`Gyms`, `Sessions`, `DiagnosticsUploads`) + one DB per tenant (`<app>_<slug>`), created by an idempotent provisioning endpoint (slug + uniqueness + rollback + idempotent-by-key).
6. systemd service for the app; `/health` endpoint for the proxy/monitor.
7. Serve update manifests + ZIPs from `wwwroot/releases/`; verify SHA server-side after upload.
8. Nightly `pg_dump`/`pg_dumpall` backups; keep the last few release ZIPs for rollback; document domain/registrar/cert-renewal date; **keep all secrets out of any shared spec.**

---

## Appendix A — Cloud API Contracts

All `/api/*` endpoints except `/api/version/latest` require the header **`X-Api-Key: <per-gym key>`** (401 if missing/invalid). JSON property names are **PascalCase** as shown. Timestamps are ISO-8601 UTC. Common errors: `401` bad key, `429` throttle (5 fails / 15 min / IP on secret-guarded endpoints), `500` server/DB error.

### A.1 `POST /api/sync` — desktop → cloud bulk upsert
**Headers:** `X-Api-Key` (req); `X-Sync-Mode: full | delta` (opt, default `full`); `Content-Type: application/json`; `Content-Encoding: gzip` (opt).
**Body** = one object with 16 arrays. Each element carries that entity's **synced columns from §20** (PascalCase). Delta mode also sends the row's `UpdatedAt`. Array keys (camelCase):
```
players, accessEvents, devices, doors, transactions, users, auditLogs,
deletedEmployees, appSettings, accessCards, qrPool, subscriptionPlans,
posShifts, freezeHistories, products, timeGroups
```
Representative element (`players[]`):
```json
{ "Id":1,"FullNameEn":"...","FullNameAr":"...","CardNo":"...","Phone":"...",
  "SubscriptionType":"...","StartDate":"2026-01-01T00:00:00Z","EndDate":"2026-02-01T00:00:00Z",
  "SubscriptionFee":25000,"Discount":0,"AmountPaid":25000,"MaxVisits":0,"UsedVisits":0,
  "IsFrozen":false,"FreezeStartDate":null,"IsDeleted":false,"CreatedAt":"...","PhotoPath":"",
  "Height":0,"Weight":0,"Notes":"","UpdatedAt":"..." }
```
**Response 200:** `{ "success": bool, "total": int, "errors": [string] }` (`success:true` even on partial; per-row failures listed in `errors`).
**Modes:** `full` = DELETE then INSERT each table (initial/reset); `delta` = `INSERT … ON CONFLICT (Id) DO UPDATE` + apply `deletedEmployees` tombstones.
**Limits:** ≤ 50 MB on the wire, ≤ 200 MB decompressed (zip-bomb guarded → `413`/`400`). After apply, master `Gyms` fleet metrics refresh; a `CloudSyncLogs` row is written.

### A.2 `POST /api/provision-gym` — create a tenant
**Headers:** `Content-Type: application/json` (no API key; guarded by the secret in the body).
**Body:** `{ "provisionSecret":str, "gymName":str, "apiKey":str, "ownerName":str?, "ownerPhone":str?, "ownerEmail":str?, "subdomain":str?, "qrPoolEnabled":bool? }`
**Response 200:** `{ "success":true, "subdomain":"basmia", "alreadyExisted":bool }`
**Errors:** `401` wrong secret, `400` missing `gymName`/`apiKey`, `429` throttle. **Idempotent by `apiKey`** (re-run returns the same subdomain). Subdomain is slugified + uniqueness-incremented; reserved `{www,admin,api}` never assigned.

### A.3 `GET /api/lock-status` — flags + lock (read-only poll)
**Response 200:** `{ "locked":bool, "lockMessage":str, "onlineEnabled":bool, "posEnabled":bool, "qrPoolEnabled":bool }`. Polled ~every 5 min; no side effects.

### A.4 `GET /api/sync-control` — force-full-sync + lock (read-and-clear)
**Response 200:** `{ "forceFullSync":bool, "locked":bool, "lockMessage":str }`. `forceFullSync` is atomically **read-and-cleared** (PostgreSQL `UPDATE … RETURNING`) so it fires once. Desktop calls this before a sync cycle; if true, it resends everything in `full` mode.

### A.5 `GET /api/version/latest` — auto-update manifest (public, no auth)
**Response 200:** `{ "version":"4.6.50", "downloadUrl":"https://…/AccessControlPro-v4.6.50.zip", "sha256":"<hex>", "sizeBytes":long, "releaseNotes_en":str, "releaseNotes_ar":str, "mandatory":bool, "minVersion":"4.0.0", "releasedAt":"…Z" }`. `404` if none published (client just skips). `Cache-Control: public, max-age=300`. Client must reject a non-HTTPS `downloadUrl` and verify `sha256` after download.

### A.6 `POST /api/diagnostics/upload` — support bundle
**Headers:** `X-Api-Key` (req); `X-Trigger: manual|auto|crash-recovery`; `X-App-Version`; `Content-Type: application/zip`.
**Body:** binary ZIP (user note, system/app/gym info, sanitized settings, network test, DB snapshot, event tail, logs).
**Response 200:** `{ "success":true, "id":int, "sizeBytes":long }`. Limit ~25 MB (`400` over). Stored `<root>/<gymId>/<timestamp>.zip`; newest ~12 per gym kept.

### A.7 `GET /api/users` — pull staff accounts
**Response 200:** array of `{ "Id":int, "Username":str, "PasswordHash":str, "DisplayName":str, "Role":str, "IsActive":bool, "Permissions":str }`. Desktop merges by `Username` (local is master; won't overwrite existing).

### A.8 `GET /api/qr-pool` — pull cloud-assigned QR codes
**Response 200:** array (server filters to `Status=1` AND `Source='Cloud'`): `{ "Code":str, "Status":int, "Source":"Cloud", "GuestName":str, "GuestPhone":str, "Reason":str, "MaxUses":int, "UsedCount":int, "ValidTo":"…Z", "DoorPermissions":"01010000", "AssignedAt":"…Z"|null }`. Desktop inserts codes it doesn't already have.

---

## Appendix B — Entity-Relationship Map (text ERD)

Authoritative shape is the **local SQL Server** schema (§20.1). `[PK]`=identity key; `→`=FK; cascade behavior in parens. The cloud PG schema mirrors these entities with looser FKs (only `AccessCards.EmployeeId → Players.Id` CASCADE is enforced there).

```
Devices [PK Id]
  └─1:N→ Doors [PK Id] (Doors.DeviceId → Devices.Id, CASCADE)
                └─1:N→ AccessEvents [PK Id] (AccessEvents.DoorId → Doors.Id)

Employees(Members) [PK Id]
  ├─1:N→ AccessCards [PK Id] (AccessCards.EmployeeId → Employees.Id, CASCADE)
  │           ├─1:N→ AccessEvents (AccessEvents.CardId → AccessCards.Id, SET NULL)
  │           └─1:N→ CardDeviceSyncs (AccessCardId → AccessCards.Id, CASCADE)
  ├─1:N→ FreezeHistories (EmployeeId → Employees.Id, CASCADE)
  └─1:N→ Transactions (Transactions.RelatedEmployeeId → Employees.Id, SET NULL)

Devices [PK Id]
  └─1:N→ CardDeviceSyncs (CardDeviceSyncs.DeviceId → Devices.Id, CASCADE)
         (junction: unique (AccessCardId, DeviceId) — one row per card×device)

Suppliers [PK Id]
  └─1:N→ PurchaseOrders [PK Id] (SupplierId → Suppliers.Id)
              └─1:N→ PurchaseOrderItems (PurchaseOrderId → PurchaseOrders.Id, CASCADE;
                                          ProductId → Products.Id)

Products [PK Id]
  ├─1:N→ PurchaseOrderItems (ProductId → Products.Id)
  └─1:N→ StockMovements (StockMovements.ProductId → Products.Id, CASCADE;
                         .PurchaseOrderId → PurchaseOrders.Id [opt];
                         .TransactionId → Transactions.Id, SET NULL [opt])

Transactions [PK Id]
  └─1:N→ StockMovements (link a POS sale to the stock-out)

Standalone (no FK — keyed by natural columns / synced by Id):
  Users, AuditLogs, DeletedEmployees(OriginalId is a soft ref, not FK),
  SubscriptionPlans, TimeGroups(unique HardwareIndex), QrPasses(unique PassCode),
  QrPool(unique Code), AppSettings(single row Id=1), LookupItems, MonitorLocks

Cloud MASTER DB: Gyms [PK Id] ──(Subdomain, DatabaseName, ApiKey)──> each tenant DB.
  Sessions(Token PK, GymId), DiagnosticsUploads(GymId) reference a gym logically (no cross-DB FK).
  Per-gym: PushSubscriptions(PlayerId → Players.Id), CloudSyncLogs (standalone).
```

**Cardinality summary:** a Member has many Cards; a Card has many Events and many per-device sync rows; a Device has many Doors; a Door has many Events; a Supplier has many POs; a PO has many Items; a Product has many Items and Movements. Everything else is a flat, independently-synced table keyed by `Id`.

---

## Appendix C — Hardware Integration (reference controller)  **[HARDWARE-SPECIFIC]**

This is what the reference product does with its specific controller (native `CareaIfc.dll` family, TCP 8000 / UDP 8101). **Re-implement all of this against your own controller's SDK/API** — the surrounding app is unaffected as long as the `IAccessControlSdk` interface contract (C.4) is preserved.

### C.1 Card write — `AddAccessCard`
Wrapper: `AddAccessCard(device, cardNo, cardPassword, openMode, openLock, permitTime, effectiveTimes=1, timePeriodIndex=0, holidayEnabled=false)`.

| Param | Type | Meaning / valid values |
|---|---|---|
| `cardNo` | string | numeric card number to program (overwrite semantics — same number updates the card) |
| `cardPassword` | string | optional PIN, usually `""` |
| `openMode` | int | 0=Ordinary (default); 1 FirstCard, 2 AlwaysOpen, 3 Patrol, 4 AntiTheft |
| `openLock` | string | **door permissions: 8 chars = 4 doors × "01"/"00"**, left-to-right = door 1..4 (first pair = first/lowest door). `"01010000"` = doors 1+2 allowed. Parse: `ioFlag[i] = openLock[i*2]=='1' ? 2 : 0` (2 = entry+exit readers on for that door, 0 = off) |
| `permitTime` | string | Valid-To as `"yyyy-MM-dd HH:mm:ss"` — **the gate-enforced expiry.** Business layer sets it to **23:59:59 of the member's EndDate**. ⚠️ **Never use a far-future fallback** (a past `now.AddYears(10)` fallback let expired members open the door — removed in 4.6.45). If your firmware rejects dates below a minimum year, clamp to that minimum **without** extending a real member's validity; better yet, simply don't sync expired/frozen members (see §6.2). |
| `effectiveTimes` | int | visit budget on the device: **1–1000 = countdown** (device decrements per swipe); **65535 = unlimited**. To *disable* a card, re-push it with a **past `permitTime`** rather than 0. |
| `timePeriodIndex` | int/string | on-device schedule index (0 = no time restriction; 1–64 = a configured time group) |
| `holidayEnabled` | bool/int | honor holiday blackout if enabled |

**Raw device-param array (native install/config calls):** a fixed 24-slot string array — `[0]="s"`, `[1]`IP, `[2]`TCPPort, `[3]`Serial, `[4]`Password, **`[5]–[15]` must be `null` (NOT `""` — empty strings crash the native parser)**, `[16]`MAC, `[17]`SubnetMask, `[18]`Gateway, `[19..20]="0.0.0.0"`, `[21]="server"`, `[22]`TCPPort, `[23]`UDPPort. *(This exact layout is controller-specific; your SDK will differ — the lesson to carry over is "watch for native null-vs-empty and fixed array sizes.")*

### C.2 Event codes
Two record dimensions: **RecordType** (what kind of event) and **EventCode** (the specific outcome).

RecordType — the reference SDK's raw indices are **swapped** into the domain enum for two pairs (fetched-records path only): SDK **Card 1 ↔ domain 0**, SDK **Button 0 ↔ domain 1**, SDK **System 4 ↔ domain 5**, SDK **Alarm 5 ↔ domain 4**; DoorSensor(2)/Software(3) unchanged. *(Without the System↔Alarm swap, system heartbeats showed as "Alarm".)* The **live-monitor** path uses pre-typed objects and needs no swap.

EventCode → meaning:
| Code | Meaning | Family |
|---|---|---|
| 1 | Card Open (granted) | Card |
| 2 | Password Open | Card |
| 3 | Card + Password | Card |
| 4 | Card Repeat (re-swipe, no new visit) | Card |
| 5 | Card Expired | Card |
| 6 | Invalid Card | Card |
| 10 | Button Open | Button |
| 20 / 21 | Remote Open / Close | Software |
| 30 / 31 | Door Sensor Open / Close | DoorSensor |
| 40–45 | Fire / Police / Gas / Magnetic / Theft / Anti-Passback | Alarm |
| 50–53 | Startup / Restart / High-Temp / UPS | System |

Rule: the controller's "status" field is only a card outcome for **Card** records; for other record types, describe by record type, not by the status number (prevents non-card rows rendering as "Card Open").

### C.3 QR reader-divisor
Some readers bit-shift/scale the scanned value. Config: `QrReaderDivisor` (default 1; the ÷16 readers set 16).
- **Encode (what you print in the QR):** `printed = poolCode × divisor`.
- **Decode (what the gate delivers to the controller):** `delivered = scanned ÷ divisor = poolCode`.
- Worked ÷16 example: poolCode `50001050` → print `800016800` → reader delivers `800016800 ÷ 16 = 50001050` → matches the enrolled pool code.
- **Range rule:** place `QrRangeStart` **below** member card numbers (e.g. pool at ~3.1M when members are ~16.7M) so an encoded visitor code never collides with a real card; the pool generator also skips any code already used by a member card. Note the reader reads **digits only** (strips leading zeros; stops at the first non-digit) — so pool codes must be plain decimals.

### C.4 SDK surface to re-implement (interface `IAccessControlSdk`)
`Initialize` / `Shutdown`; `SearchDeviceAsync` (UDP discovery); `InitializeDevice(device, doorCount)`; `ReadDeviceTime` / `CalibrateTime` (**keep clock sync — Valid-To enforcement depends on it**); `UpdateIP`; `AddAccessCard` (C.1) + `WriteCardViaConnectMain` (TCP fallback) + `DeleteCardViaConnectMain`; `GetDeviceInfo`; `FetchAllRecordsAsync` (pull events, all record types) + legacy `GetRecords`; `SetOpeningHours(timeNum, timePieces)`; `SetAntiPassback`; `SetDoorDelay`; `SetDoorPassword`; `RemoteOpenDoor` / `RemoteCloseDoor`; `TriggerAlarm` / `GetFireAlarmStatus`; `StartMonitoring(devices, onEvent)` / `StopMonitoring` / `Pause` / `Resume` (live stream; keepalive re-arms every ~10 s; only one monitor process at a time — see `MonitorLocks`).

**To port to different hardware:** rewrite this wrapper (P/Invoke a different DLL or call a REST/SOAP API); re-map `openLock` encoding, `effectiveTimes` semantics, `timePeriodIndex`, RecordType/EventCode values, and the QR divisor to your device; keep clock calibration and the "never sync expired/frozen; expiry = Valid-To at end-of-day" rules. Everything above the interface stays unchanged.

---

*End of specification. This describes the full feature surface + data model + API contracts + hardware integration + infrastructure of the reference product. Implement against your own hardware SDK where marked **[HARDWARE-SPECIFIC]**, and substitute your own domain/server/credentials for the infrastructure in Section 21. No passwords, API keys, or DB credentials are included by design.*
