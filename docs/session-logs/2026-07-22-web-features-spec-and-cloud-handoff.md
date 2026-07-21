# Session log — 2026-07-22 — WEB-FEATURES.md spec + cloud-infra handoff for the 2nd ("face") project

Prior log: `docs/session-logs/2026-07-18-v4.6.59-heap-histogram-diagnostics.md`

No release this entry — documentation + infra handoff. (This session as a whole also shipped 4.6.54/4.6.55/4.6.58/4.6.59 — each has its own log.)

## 1. WEB-FEATURES.md — full web/cloud feature spec by ROLE (committed `9c6fd5a`, pushed)
Mustafa asked for a complete "prompt" of the **web version** (Blazor cloud portal + PWA), split by the three roles, to hand to another AI. Deep-mapped the actual `src/AccessControlPro.Web` app (an Explore agent read every `.razor` page + `Program.cs` + auth/session + subdomain middleware) and wrote **`WEB-FEATURES.md`** (repo root, ~290 lines, secret-free, English). Sections:
- **Foundation:** multi-tenant subdomain→`gymcloud_{subdomain}` DB resolution; server-side `Sessions` (HttpOnly cookie, 24h staff / 90-day player); login per role; brute-force lockout; SuperAdmin impersonation.
- **Role 1 Gym Owner** (per-gym back office): 12 screens, each marked **read-only mirror vs actionable**. Actionable web surfaces are ONLY: Users (46-permission staff CRUD), QR guest passes, Dashboard force-sync, password. Everything else mirrors desktop data.
- **Role 2 Player PWA:** `/my` (subscription/card-QR/visits/payments/history) + `/my/visits`; installable PWA; **read-only except enabling push renewal reminders**; Blazor Server ⇒ not usable offline beyond the cached shell.
- **Role 3 SuperAdmin** (vendor fleet): 8 screens; the real controls = gym CRUD (auto-creates tenant DB + QR pool), remote lock (payment enforcement), feature flags, extend/delete, diagnostics download/purge, health, revenue, data-quality scan.
- **Data + API:** which DBs each role touches (master `gymcloud` vs per-gym), every `/api/*` endpoint (sync/users/qr-pool/sync-control/lock-status/provision-gym/diagnostics/version/auth), and the Web Push renewal-reminder background job.
- ⭐**Key framing:** the cloud is a **read-only mirror**; the **desktop app is the source of truth** and pushes up via `/api/sync`. Any AI studying it needs that up front.

Pairs with `SOFTWARE-FEATURES.md` (desktop). Both are secret-free handoff docs in the repo root.

## 2. Cloud-infra handoff for the 2nd ("face") project (given in-chat; facts saved to memory)
Mustafa is building a **second, different product on the SAME VPS/Postgres/nginx** (different domain) via another cloud AI agent, which asked for DB creds + current setup + how the cloud works. Re-verified live facts (read-only SSH) and produced a full handoff. Saved to memory `reference_second_instance_face.md` (2026-07-22 UPDATE). Highlights:
- **`facecloud` cert already exists** → the 2nd project is underway (subdomains `face.basmia`/`face.admin.hmtech.solutions`, exp 2026-10-10). They chose `face.<x>.hmtech.solutions` (own cert, since the `*.hmtech.solutions` wildcard is one-label-only).
- **DB conn model:** app connects as **`postgres` superuser**, `Host=localhost;Port=5432;Database=gymcloud`, TCP scram-sha-256; password in `/var/www/gymapp/appsettings.json`. **I refused to paste the prod password** into the handoff (leak risk) — recommended the 2nd project create its own `faceuser` role w/ `CREATEDB` + fresh password. This is the correct isolation.
- **Deploy = bare-metal .NET/systemd** (Docker NOT installed; no git repo on the server = scp the built self-contained zip). dotnet 8.0.25.
- **Master `gymcloud`** + registry tables (Gyms/Sessions/CloudSyncLogs/DiagnosticsUploads) + **10 tenant DBs**. ⚠️ Tenant DB name ≠ subdomain — resolve via `Gyms.DatabaseName` (basmia→`gymcloud_drag`).
- **Live fleet 2026-07-21 (actively syncing):** 10 gyms, ~**7,350 players** (basmia 2663, Power 1000, Fursan 950, Classic 826, phisphor 581, O2 507, ALI 485, MAW 239, noamani 105, harith 0*). `/api/sync` continuous, `/api/provision-gym` works, 34 diagnostics bundles.
- **Isolation recipe** for the 2nd app: own DBs (`facecloud`+`facecloud_{x}`), own role, port 5001, own systemd unit, **separate nginx file + `nginx -t` + reload** (nginx = the only shared break-risk). Full detail in the memory file.

## Still deferred (unchanged)
Read the first heap histogram from harith/Power → fix the 32-bit OOM leak at source; enable Windows auto-login on gym PCs (for the 5PM/10PM reboot); basmia on-site gate QR test; events auto-capture; Finance-outstanding pagination; delete dead `EnsureMigrationDefaultsResolvedAsync`/`IsMigrationDefault`.
