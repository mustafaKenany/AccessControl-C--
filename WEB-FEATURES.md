# Gym Management — WEB / CLOUD Application: Complete Feature Specification by Role

> **How to use this document (prompt for another AI):**
> You are given the complete functional specification of the **web (cloud) side** of a gym access-control &
> management product. It is a **Blazor Server** web app plus an installable **PWA**, multi-tenant (one gym per
> subdomain), that mirrors data pushed up from a Windows desktop app and adds owner/member/vendor portals.
> Study it and understand how it works: the three roles, what each can see and do, the multi-tenant model, the
> API the desktop uses, and which screens are read-only mirrors vs actionable. No secrets (passwords, keys,
> tokens) are included — only where a value lives in configuration is noted by its config key name.

---

## 0. What the web system is

- **Cloud portal + PWA** built with **Blazor Server (.NET 8, InteractiveServer render mode)** over **PostgreSQL** (Npgsql).
  Hosted behind a reverse proxy; the app listens on an internal HTTP port.
- **UI is Arabic / RTL only** in the current build (a bilingual `L("en","ar")` helper exists but always renders Arabic; the language toggle is a no-op). A re-implementation may keep both languages.
- **It is primarily a READ-ONLY MIRROR.** The source of truth is the **desktop app** installed at each gym, which
  **pushes** its data up via a sync API. Almost all member/finance/event/device data shown in the cloud is
  view-only; data entry happens on the desktop and syncs up. The genuinely *actionable* web features are called out explicitly below.
- **Three roles:**
  1. **Gym Owner** (gym staff: roles `Owner` / `Admin` / `User`) — per-gym back-office portal.
  2. **Player** (gym member) — a personal PWA ("my subscription" phone app).
  3. **SuperAdmin** (the vendor) — a fleet-management portal across all gyms.

---

## 1. Multi-tenancy, authentication & sessions (shared foundation)

### 1.1 Subdomain → tenant-database resolution
- A middleware runs first on every request and takes the **leftmost label of the host** as the gym subdomain
  (e.g. `basmia` from `basmia.hmtech.solutions`; also works with `*.localhost` in development).
- Reserved labels `www`, `admin`, `api`, and bare IP addresses resolve to **"root / no gym"**. The label `admin`
  marks the request as the **SuperAdmin host**.
- A real subdomain is looked up in the **master database `gymcloud`**, table **`Gyms`**, by `Subdomain` (must be
  `IsActive = TRUE`). The resolved gym (id, name, subdomain, database name, api key) is attached to the request.
- **Each gym has its own physical PostgreSQL database** named **`gymcloud_{subdomain}`**. The master `Gyms` row
  stores that `DatabaseName`; the data layer swaps the `Database=` in the connection string per request. The
  master registry (`Gyms`, `Sessions`, `CloudSyncLogs`, `DiagnosticsUploads`) lives in `gymcloud`.

### 1.2 Sessions
- **Server-side sessions**: a `Sessions` table (master DB) holds each session; the browser only holds an opaque
  256-bit random token in an **HttpOnly, SameSite=Lax cookie** (`Secure` on HTTPS). JavaScript never sees the token.
- On each request the token is validated (not revoked, not expired), and `LastSeenAt` / `LastIp` / `LastUserAgent`
  are refreshed.
- **Lifetimes:** 24 hours for staff/owner/superadmin; **90 days for a Player** (so the installed PWA rarely re-prompts).
- Login pages POST JSON to server endpoints; the server sets the cookie and returns a redirect target, then the
  page does a full navigation so the session hydrates. **Redirects:** Player → `/my`, Owner/Admin → `/dashboard`,
  SuperAdmin → `/superadmin/dashboard`.
- **Brute-force lockout:** 5 failed attempts within 15 minutes blocks that identifier (`owner:{username}`,
  `player:{phone}`, `superadmin:{ip}`, `provision:{ip}`). Applies to all logins and the provisioning secret.
- **Logout** revokes the session row and clears the cookie.

### 1.3 Login per role
- **Gym Owner:** on the gym's own subdomain, `/login` ("Owner Login" tab) with **username + password** (passwords
  are BCrypt-hashed). Scoped to that gym's `Users` table; the gym must be active. Roles: `Owner`, `Admin`, `User`
  (the portal treats `Owner`/`Admin` as full access; a plain `User` currently gets no portal access).
- **Player:** same `/login` page ("Player Login" tab) with **phone number + last 4 digits of their card** (no
  password). Matches a row in `Players` (not deleted) whose card number ends with those 4 digits.
- **SuperAdmin:** a **separate** login page at `/superadmin` (and `/superadmin/login`), **username + password** taken
  from **server configuration** (`SuperAdmin:Username`, `SuperAdmin:PasswordHash`) — never stored in a gym DB.
- **Impersonation:** a SuperAdmin can "Login as Owner" for any gym (from the gym-details page); this swaps their
  session for a support-view Owner session of that gym. They must re-login to regain SuperAdmin powers.
- **Change password** (`/change-password`): any authenticated staff user can change their own password (verify
  current, min 6 chars). Effectively a staff feature.

---

## 2. ROLE 1 — GYM OWNER portal (per-gym back office)

Sidebar: Dashboard, Players, Events, Finance, Devices, Logs, Deleted, QR Pass, Backup, Users, Gym info, Password,
Logout. **All owner pages are gated on the Owner/Admin role only** — granular per-permission enforcement is *not*
wired in the cloud yet (the 46-permission model is used only when editing staff on the Users page).

**Actionable owner surfaces:** Users (full staff CRUD), QR Pass (create/deactivate guest passes),
Dashboard "Force full sync", and password change. **Everything else is view/search/filter only** (a mirror of the desktop).

### 2.1 Dashboard — `/dashboard`
- **Sync bar:** last sync time from the gym PC + status badge (Success / Failed / Partial); alert counting
  partial/failed syncs in the last 24 h (expandable to the latest error).
- **Action — Force full sync:** sets a flag the desktop reads on its next poll and re-uploads everything.
- **Action — Refresh.**
- **8 stat cards:** Total Players, Active, Expiring Soon (≤7 days), Expired, Frozen, Today's Entries, Monthly
  Revenue, Outstanding Balance.
- **Recent Events** (last 10) and **Expiring Soon** (next 7 days) lists. View-only.

### 2.2 Players — `/players` (read-only)
- Member tiles (name + status-colored ring); tap → player profile.
- **Search** by name / phone / card; **status filter** (All / Active / Expiring / Expired / Frozen); **"expires-in"
  period filter** (Today … Last 6 Months, with date-range tooltips); pagination (20/page). No add/edit/delete.

### 2.3 Player Profile — `/player/{id}` (read-only + one share action)
- Header: avatar, name, status, card, phone, Days-Left, Visits.
- **Action — Share Login via WhatsApp** (sends the member the portal URL + their phone + last-4 card digits).
- **Six read-only tabs:** Subscription (type, dates, fee/discount/paid/remaining, card, visit progress),
  Payments (this member's transactions + totals), Renewals (create/renew/update audit), Freezes (freeze/unfreeze
  audit), Cards (current cards + assign/remove/reassign/sync history), Visits (last 100 access events with door + direction).

### 2.4 Events — `/events` (read-only)
- Access-events table: time (local), device, door, card number (QR badge for pool cards), resolved player name,
  event type (Card / Button / Remote / Alarm / Door-Open / Door-Close). Period filter (default Today); search by
  player/card/door spans all history; "load more" pagination (100/batch).

### 2.5 Finance — `/finance` (read-only)
- 4 cards: Total Revenue, Total Expenses, Net Profit, Outstanding (all-time unpaid). Period filter (default this
  month) + type filter (Income/Expense) + search (spans all dates). Transactions table (date, type, category,
  amount, description, recorded-by), pagination 20/page. No entry — mirror of desktop finance.

### 2.6 Devices — `/devices` (read-only)
- One card per controller: name, serial, IP, door count, door names. No control actions (no open/close/edit).

### 2.7 Logs — `/logs` (read-only)
- Audit-log table: time, action (color-coded), entity type/id, details, performed-by. Period filter (default this
  month) + full-text search across action/entity/details/performer; server-side pagination.

### 2.8 QR Pass — `/qr-pass` (ACTIONABLE — guest passes)
- **Create guest pass:** guest name (required) + optional phone; fixed validity **2 uses · 24 hours**. It draws an
  unused code from the gym's cloud QR pool that is already confirmed on the gate; errors clearly if no cloud codes
  are loaded yet. Shows the QR image, the device code (honoring a per-gym reader "divisor" for bit-shifting
  readers). **Actions:** Share via WhatsApp, Download PNG.
- **Active Passes table:** name, code, uses (used/max), valid-until, status (Active / Deactivated / Expired /
  Used-up) + **Deactivate** button. Available only if the SuperAdmin enabled the QR pool for this gym.

### 2.9 Users — `/users` (FULLY ACTIONABLE — staff management)
- Stats: total / active / inactive.
- **Create user:** username, display name, password (min 6), role (Admin / User), and a full **46-permission**
  checklist grouped by area (App Access, Dashboard, Players, Devices, Doors, Events, Finance, Cash Flow, POS, Time
  Groups, Subscription Plans, Logs, Deleted Records, Monitor, Data Migration, QR Pass, Backup, Admin) with
  select-all / clear-all. Duplicate usernames rejected.
- Per-user: **Edit** (name, role incl. Owner, active flag, permissions), **Reset Password**, **Deactivate** (reason
  required), **Activate**, **Delete permanently** (reason required). Owner-role users can't be deactivated/deleted.
- Every action writes an audit row. Cloud-created staff **sync back down** to the desktop (via the users pull API),
  so staff can be managed from either side.

### 2.10 Backup — `/backup` (read-only)
- "Backup status is synced from the gym PC." Shows last sync time + a Sync-History table (recent sync runs:
  time, type, status, details). No backup can be triggered from the web.

### 2.11 Deleted — `/deleted` (read-only)
- Archived (soft-deleted) members: original id, name, card, phone, delete reason, deleted-by, deleted-at. Search +
  pagination. No restore.

### 2.12 Gym info — `/admin/gym` (read-only)
- This gym's own record: name, subdomain, cloud expiry + days remaining + status, owner name/phone/email, support
  note. (Different from the SuperAdmin fleet manager.)

---

## 3. ROLE 2 — PLAYER (member) PWA

A personal phone app for the member. Nav: My Subscription, Visits, Password, Logout. **Read-only except one
action** (enabling push renewal reminders).

### 3.1 My Subscription — `/my`
- Header: photo/avatar, name, status (Active / Frozen / Expired, computed from end date + frozen flag).
- Stat cards: Subscription Type, Days Remaining (color-coded when low), Visits Remaining (if visit-limited).
- **Five tabs:**
  - **Overview:** visit progress bar; start/end dates, card number, phone, fee, paid, remaining, visits used/max,
    height, weight, frozen-since, notes.
  - **My Card:** renders a **QR of the member's card number** + the number ("show at entrance"); warns if not
    active. Also the **Renewal Reminders** opt-in.
  - **Payments:** the member's transactions (last ~3 months).
  - **Visits:** 3 stat cards (total / this-month / this-week) + recent entries (door, time, type).
  - **History:** the member's freeze/unfreeze history.
- **Action — Enable reminders** (the only actionable control): subscribes the browser to push and stores the
  subscription; hidden when push isn't configured.

### 3.2 Visits — `/my/visits` (read-only)
- Full visit history for the member's card: 3 stat cards + paginated table (date, time, door, type).

### 3.3 PWA / phone-app aspects
- **Installable PWA** (web manifest: standalone, portrait, themed, app icons incl. maskable, a "My Subscription"
  shortcut; installable on Android and iOS). Start URL is the member dashboard.
- **Service worker** caches only the **static shell** (CSS, icons, manifest) and shows an **offline fallback page**
  when disconnected — because it is Blazor **Server**, the interactive app itself needs a live connection and is
  **not usable offline** beyond the shell. It never caches the framework, live-connection, or API paths. It handles
  push and notification-click events (opens the member dashboard).
- **Push notifications:** the member's installed PWA/browser receives renewal reminders (see §5.3).

---

## 4. ROLE 3 — SUPERADMIN (vendor fleet portal)

Separate purple-themed portal; every page gated on the SuperAdmin role. Reads the **master DB `gymcloud`** and,
where noted, opens individual tenant DBs. This is where the vendor runs the business.

### 4.1 Dashboard — `/superadmin/dashboard` (read-only)
- 6 cards: Total Gyms, Active, Expired, Total Players (summed), Monthly Revenue (summed subscription prices),
  Active Sync (24 h). Recent sync logs + a gym-overview table.

### 4.2 Gyms — `/superadmin/gyms` (FULLY ACTIONABLE — fleet CRUD)
- **Add / Edit gym:** name, subdomain, owner name/phone/email, monthly subscription price, expiry date, notes, and
  **QR-pool settings** (enable + pool size + monthly fee). Adding a gym **auto-generates its API key** (format
  `HMT-{subdomain}-{short-guid}`), **creates its tenant database** `gymcloud_{subdomain}`, computes a
  non-overlapping QR code range (split local/cloud), and generates the cloud QR pool rows.
- **Per-gym actions:** View (→ details), Edit, **Extend subscription** (+1/+3/+6/+12 months), **Toggle Active**
  (deactivation blocks that gym's logins), **Delete permanently** (drops the tenant database), **WhatsApp the owner**.
- **Renewal reminders panel:** gyms expiring within 30 days with owner/phone/days-left; "Remind all" builds
  per-gym WhatsApp messages.

### 4.3 Gym Details — `/superadmin/gym/{id}` (ACTIONABLE control panel)
- Gym info incl. subdomain, database name, expiry, and the **API key** (to hand to that gym's desktop software),
  QR range/pool size.
- **Login as Owner** (impersonate — support view).
- **Remote Lock (payment enforcement):** editable "message shown to the gym", **Lock / Unlock / Save message**. The
  desktop reads this and blocks itself within ~5 minutes (and auto-locks after being offline 7+ days). Data is
  never touched — this is a soft block for non-payment.
- **Feature flags** (owner can't change): toggle **Online subscription**, **POS/Cashier module**, **QR pool**;
  the desktop applies these within ~5 minutes.
- **QR pool** (if enabled): status, size, code range, local/cloud split, monthly fee, editable **reader divisor**,
  and live counts of available / assigned / used cloud codes.
- **Read-only drill-down** into the tenant DB: player/event/revenue stat cards, recent players, recent events,
  recent transactions, sync history.

### 4.4 Gym Health — `/superadmin/gym-health` (read-only)
- Per-gym vital signs: players, active, members-without-card, last event, last sync, last diagnostics, **app
  version** (flags outdated), expiry, recent crashes (14 days), and a **Green/Yellow/Red health score** with reason
  tooltips; summary cards. Refresh only.

### 4.5 Health — `/superadmin/health` (read-only)
- Master-DB / system stats: database size, active connections, uptime, table counts, totals (players/events/
  transactions/syncs), largest tables, last-sync-per-gym, recent sync errors. Refresh only.

### 4.6 Revenue — `/superadmin/revenue` (read-only + reminders)
- Cards: total monthly revenue, yearly projection, overdue/expired count, paying-gyms count. Breakdown of
  subscription vs QR-pool revenue; revenue-by-gym table; overdue-payments table with **WhatsApp "Remind"** links.

### 4.7 Diagnostics — `/superadmin/diagnostics` (ACTIONABLE — support bundles)
- Lists uploaded diagnostic bundles (gym, time, trigger [auto / manual / crash-recovery], app version, size) with
  storage totals. **Actions:** Download a bundle, Delete one, bulk **Delete > 30 days**. (The desktop uploads these
  bundles automatically on crash / on a schedule / on demand.)

### 4.8 Data Quality — `/superadmin/data-quality` (read-only integrity scan)
- Opens **every active tenant DB** and reports per-gym integrity: players, migrated players, cards, **dangling
  cards** (point to deleted players), **duplicate cards**, **orphan freezes**, active subscription plans (flags 0),
  available cloud QR codes; a Clean / issues verdict per gym + an action guide. Diagnosis only (no fix button).

---

## 5. Data model, API & background jobs

### 5.1 Databases by role
- **Master `gymcloud`** (SuperAdmin + shared): `Gyms` (the registry: api key, database name, active flag, expiry,
  monthly price, lock flag + message, feature flags online/POS/QR, QR-pool config, fleet-health counters,
  force-full-sync flag), `Sessions`, `CloudSyncLogs`, `DiagnosticsUploads`.
- **Per-gym `gymcloud_{subdomain}`** (Owner + Player): `Players`, `AccessEvents`, `AccessCards`, `Doors`, `Devices`,
  `Transactions`, `Users`, `AuditLogs`, `DeletedEmployees`, `QrPool`, `QrPasses`, `AppSettings`, `SubscriptionPlans`,
  `FreezeHistories`, `PosShifts`, `Products`, `TimeGroups`, `PushSubscriptions`.

### 5.2 API endpoints the web app exposes (the desktop is the client)
All data endpoints authenticate with an **`X-Api-Key`** header that maps to a specific active gym.
- `GET /health` — liveness (no auth).
- **`POST /api/sync`** — the desktop **pushes** data (gzip; large-body cap). Modes: `full` (wipe + insert) or
  `delta` (upsert + apply delete tombstones). Upserts all the per-gym tables and writes a sync-log row + updates
  the gym's fleet-health counters.
- `GET /api/users` — desktop **pulls** cloud-created staff (so web-added users appear on the desktop).
- `GET /api/qr-pool` — desktop pulls cloud-assigned QR codes.
- `GET /api/sync-control` — read-and-clear the force-full-sync flag; also returns lock state.
- `GET /api/lock-status` — read-only lock + feature flags (locked, message, online, POS, QR-pool).
- **`POST /api/provision-gym`** — the desktop setup wizard auto-creates a gym; gated by a server-config
  provisioning secret (brute-force protected), idempotent by API key, builds a unique subdomain, creates the tenant
  DB + master row (1-year expiry).
- **`POST /api/diagnostics/upload`** — desktop uploads a logs+snapshot ZIP (size cap; keeps the most recent per
  gym). `GET /api/diagnostics/download/{id}` — SuperAdmin only.
- `GET /api/version/latest` — public auto-update manifest the desktop polls.
- Auth: `POST /api/auth/login`, `/api/auth/superadmin-login`, `/api/auth/logout`, `/api/auth/impersonate`.

### 5.3 Push notifications (renewal reminders)
- A **background service** runs shortly after startup then every 12 hours. For each active gym it finds members
  whose subscription ends within ~3 days and who have a stored push subscription not recently nudged, and sends a
  bilingual renewal reminder via Web Push (VAPID). Dead subscriptions are dropped.
- **Configuration-gated:** does nothing until VAPID keys are present in configuration (`WebPush:PublicKey` /
  `PrivateKey`). Subscriptions are stored per gym; the member opts in from their dashboard's "My Card" tab.

---

## 6. Behavior notes / gotchas a re-implementation must respect
- The cloud is a **read-only mirror** for members, events, finance, devices, logs, deleted, and backup — all data
  entry is on the desktop and syncs up. Web-*actionable* surfaces are only: **staff Users CRUD**, **QR guest
  passes**, **Force-full-sync**, **push opt-in**, and the **entire SuperAdmin portal** (gym CRUD, remote lock,
  feature flags, extend, delete, diagnostics).
- **Granular per-permission enforcement is not yet applied** on owner pages (they gate on Owner/Admin only), even
  though a full 46-permission model exists for editing staff.
- **Remote lock is a payment-enforcement soft block**, not a data operation; it also triggers automatically after a
  gym is offline for 7+ days.
- **Deactivating/deleting a gym blocks the next login** (via the active flag); it does not kill live sessions.
- **Multi-tenant isolation is by physical database per gym**; the master DB only holds the registry, sessions, sync
  logs, and diagnostics. A new gym = new subdomain + new `gymcloud_{subdomain}` database.
- **Player auth has no password** (phone + last-4-of-card) and a long (90-day) session, tuned for a low-friction
  installed PWA.
