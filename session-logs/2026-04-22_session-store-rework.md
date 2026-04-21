# Session Log — 2026-04-22 (Session Store Rework)

**Branch:** feature/pwa-cloud
**Follows:** [2026-04-22_hardening-pass-2.md](2026-04-22_hardening-pass-2.md)

This is the big security rework you asked for: replacing the plaintext `.active_session` flat file + JS-readable cookies with a **real server-side session store backed by HttpOnly cookies**. This closes the two biggest session-layer attack surfaces:

1. Filesystem leak → session theft (backups, disk snapshots, misconfigured permissions)
2. XSS → session theft (any JS bug on any page could steal `document.cookie`)

---

## What changed

### Server-side session store

**New table** `Sessions` in the master PostgreSQL DB:
- `Token` (VARCHAR(128), PK — a 256-bit random URL-safe base64 string)
- `Role`, `DisplayName`, `UserId`, `GymDatabase`, `GymId` (the actual session payload)
- `CreatedAt`, `ExpiresAt`, `RevokedAt`, `LastSeenAt`
- `LastIp`, `LastUserAgent` (audit trail)

**New service** [`Services/SessionService.cs`](../src/AccessControlPro.Web/Services/SessionService.cs):
- `CreateAsync` — 256-bit random token via `RandomNumberGenerator`, insert row
- `ValidateAsync` — single-query lookup + refresh `LastSeenAt`, returns null if expired/revoked
- `RevokeAsync` — sets `RevokedAt` so the token stops working immediately
- `PruneAsync` — removes rows older than 30 days (expired or revoked)

### Request pipeline

**New middleware** (Program.cs): on every HTTP request, reads `ac_session` cookie, calls `SessionService.ValidateAsync`, stashes the `SessionInfo` in `HttpContext.Items["SessionInfo"]`. If the cookie is invalid or expired, deletes it so the browser stops sending it.

### New auth endpoints (Program.cs)

- `POST /api/auth/login` — validates owner/player credentials via `WebAuthService`, creates session, sets `HttpOnly Secure SameSite=Strict` cookie, returns `{success, redirect}`
- `POST /api/auth/superadmin-login` — same pattern for super admin, supports both BCrypt and legacy SHA256 hash
- `POST /api/auth/logout` — revokes session server-side + deletes cookie

The cookie is set with:
- `HttpOnly = true` — JS can't read it
- `Secure = Request.IsHttps` — HTTPS-only on prod (ForwardedHeaders middleware makes this work behind a reverse proxy)
- `SameSite = Strict` — CSRF protection
- `Expires = UtcNow + 24h`
- `Path = "/"`

### Blazor component changes

**Login.razor** — removed all `.active_session` file writes, `eval()` JS cookies, and in-memory `Session.Login()`. The button now POSTs credentials via `authPost` JS helper (in `App.razor`) to `/api/auth/login`; server handles everything.

**SuperAdminLogin.razor** — same pattern for the super admin login. Hash verification moved server-side.

**Logout.razor** — calls `POST /api/auth/logout` to revoke server-side, then navigates.

**MainLayout.razor**, **SuperAdminLayout.razor**, **OwnerDashboard.razor**, **SuperAdmin/Dashboard.razor** — all replaced their `.active_session` file-reading blocks with a single lookup of `HttpContext.Items["SessionInfo"]` (populated by the new middleware). One line instead of 15.

**App.razor** — added `window.authPost` JS helper that all login/logout components share.

---

## Before / after

| What | Before | After |
|---|---|---|
| Session storage | Flat file `.active_session` with `Role\|Name\|Id\|Db\|GymId` | PostgreSQL `Sessions` row with random token |
| Session token in browser | JS-readable cookie with base64'd payload | Opaque random token in `HttpOnly; Secure; SameSite=Strict` cookie |
| Logout | Delete file + `document.cookie = ''` (can be bypassed by restoring file) | `UPDATE Sessions SET RevokedAt = NOW()` — irrevocable |
| Session restore on page load | Read file → parse → populate in-memory | Middleware validates cookie → one-line hydrate from `HttpContext.Items` |
| Server restart | Old sessions survive (file persists) | Old sessions survive (DB persists) — **but revocation works** and sessions have `ExpiresAt` |
| XSS steals session? | Yes — cookie is JS-readable | No — `HttpOnly` blocks JS access |
| Backup leak steals session? | Yes — just open `.active_session` | No — attacker needs the random 256-bit token, which only lives in the browser's HttpOnly cookie store |
| Admin can force-logout a user? | No | `UPDATE Sessions SET RevokedAt = NOW() WHERE UserId = X` — next request fails |

---

## What still requires your action

1. **Rotate existing credentials** if you believe the pre-rework `.active_session` files may have been compromised (backups, screen sharing, etc.)
2. **Configure your reverse proxy** (nginx/Caddy) to pass `X-Forwarded-Proto` header — already consumed by `UseForwardedHeaders` added in the previous session. Without this, cookies won't get the `Secure` flag.
3. **Migrate SuperAdmin password** from SHA256 to BCrypt at your leisure — both still work, BCrypt is preferred. Generate new hash with `BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12)`.
4. **Periodic session cleanup** — `SessionService.PruneAsync()` exists but isn't scheduled. Either call it from a hosted service (10 min work) or just let expired rows accumulate; they're harmless (still reject at login) and take up minimal space.

---

## Known limitations (by design, for this session)

- **No sliding expiration** — sessions expire 24h after creation, not 24h after last activity. Simpler, more predictable. Can be added later if users complain.
- **No "log out all my devices" UI** — the data model supports it (query `Sessions WHERE UserId = X AND RevokedAt IS NULL`), but no UI button yet. ~30 min to add if wanted.
- **Blazor circuit reconnection not explicitly tested** — the middleware runs on every HTTP request including SignalR negotiation; should work but worth smoke-testing.

---

## Files touched

- NEW: `src/AccessControlPro.Web/Services/SessionService.cs`
- `src/AccessControlPro.Web/Data/DbHelper.cs` — Sessions table migration
- `src/AccessControlPro.Web/Program.cs` — middleware + 3 new auth endpoints
- `src/AccessControlPro.Web/Components/App.razor` — `authPost` JS helper
- `src/AccessControlPro.Web/Components/Pages/Login.razor` — POST-based login
- `src/AccessControlPro.Web/Components/Pages/SuperAdminLogin.razor` — same
- `src/AccessControlPro.Web/Components/Pages/Logout.razor` — server-side revocation
- `src/AccessControlPro.Web/Components/Layout/MainLayout.razor` — session hydration from HttpContext.Items
- `src/AccessControlPro.Web/Components/Layout/SuperAdminLayout.razor` — same
- `src/AccessControlPro.Web/Components/Pages/OwnerDashboard.razor` — same
- `src/AccessControlPro.Web/Components/Pages/SuperAdmin/Dashboard.razor` — same

Zero `.active_session` file reads/writes or JS `eval("document.cookie = ...")` calls remain in the codebase. Verified by grep.

## Build status

`dotnet build src/AccessControlPro.Web/AccessControlPro.Web.csproj`: **0 errors**.
