# Subdomain Migration — Deploy Guide

Step-by-step to put `basmia.hmtech.solutions/login` (and any future
`<gymname>.hmtech.solutions`) into production. Apply in the order shown.

## Status of the code (already in the repo)

✅ Database column `Gyms.Subdomain` exists in master DB
✅ SuperAdmin Gym-create form already has a Subdomain field
✅ `GymDbHelper.ResolveGymBySubdomainAsync()` — looks up a gym by its subdomain
✅ `GymSubdomainMiddleware` — extracts subdomain from request host, resolves gym, stores in `HttpContext.Items["CurrentGym"]`
✅ `WebAuthService.OwnerLoginAsync` and `PlayerLoginAsync` accept an optional `GymInfo`
✅ `/api/auth/login` passes the resolved gym to the auth service
✅ Login.razor shows the resolved gym name OR a warning for root-domain login

## What's left to make it live (4 deployment steps)

### 1. Wildcard DNS — add a record in Hostinger

In your Hostinger control panel → DNS Zone Editor for `hmtech.solutions`:

```
Type:  A
Host:  *
Value: 89.116.39.155
TTL:   300 (5 minutes)
```

Existing records:
- `hmtech.solutions` (root A record) — leave as is
- `www.hmtech.solutions` — leave as is

After saving wait 5–10 minutes for DNS to propagate. Test:
```bash
nslookup basmia.hmtech.solutions
# Should return 89.116.39.155
```

### 2. Wildcard SSL certificate (Let's Encrypt, free, auto-renewing)

A wildcard cert (`*.hmtech.solutions`) needs the DNS-01 challenge method.

```bash
ssh root@89.116.39.155

# Install certbot if not present
apt-get update && apt-get install -y certbot

# Request wildcard cert + root cert in one go
certbot certonly --manual \
  --preferred-challenges dns \
  --email mustafa.kenany2022@gmail.com \
  --agree-tos \
  --no-eff-email \
  -d "hmtech.solutions" \
  -d "*.hmtech.solutions"
```

Certbot will pause and ask you to **add a TXT record** to your DNS to prove
you own the domain. Example output:

```
Please deploy a DNS TXT record under the name:
_acme-challenge.hmtech.solutions

with the following value:
ABCD1234XYZ...
```

Go to Hostinger DNS panel, add:
- Type: TXT
- Host: `_acme-challenge`
- Value: (paste the long string)
- TTL: 300

Wait 2 minutes (some DNS providers cache), then press Enter back in the certbot
terminal. It will verify and issue the cert.

Output cert files land at:
```
/etc/letsencrypt/live/hmtech.solutions/fullchain.pem
/etc/letsencrypt/live/hmtech.solutions/privkey.pem
```

These cover BOTH `hmtech.solutions` AND any `*.hmtech.solutions`.

**Auto-renewal:** because the DNS challenge is manual, you'll need to renew
every ~90 days by re-running the command above. Schedule a calendar reminder
for 60 days out. (Eventually we can automate this with a DNS-API plugin.)

### 3. Update Nginx config

```bash
nano /etc/nginx/sites-available/gymapp
```

Replace contents with (assumes you have the SSL cert at the standard path):

```nginx
# HTTP → HTTPS redirect for all hosts
server {
    listen 80;
    listen [::]:80;
    server_name hmtech.solutions *.hmtech.solutions;
    return 301 https://$host$request_uri;
}

# Main app (root + all gym subdomains)
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name hmtech.solutions *.hmtech.solutions;

    ssl_certificate     /etc/letsencrypt/live/hmtech.solutions/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/hmtech.solutions/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;

    # Pass the real host through so Blazor middleware can extract the subdomain.
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;

    # Blazor Server SignalR — needs upgrade for the WebSocket
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";

    location / {
        proxy_pass http://127.0.0.1:5000;
    }
}
```

Validate + reload:

```bash
nginx -t
systemctl reload nginx
```

### 4. Deploy the new web build + run the backfill SQL

```bash
# Stop the service
systemctl stop gymapp

# Backup production appsettings.json (don't lose it during the upload)
cp /var/www/gymapp/appsettings.json /tmp/appsettings.backup.json
```

Upload the contents of `publish/web-deploy/` from your PC to `/var/www/gymapp/` via FileZilla/WinSCP/scp (overwrite).

```bash
# Restore production config and start
cp /tmp/appsettings.backup.json /var/www/gymapp/appsettings.json
chmod +x /var/www/gymapp/AccessControlPro.Web
systemctl start gymapp
systemctl status gymapp

# Run the Basmia subdomain backfill
sudo -u postgres psql -f /root/backfill-basmia.sql

# (Upload backfill-basmia.sql to /root/ first via FileZilla.)
```

## Verify it all works

| URL | Expected behaviour |
|---|---|
| `https://hmtech.solutions` | Public landing page (with the language toggle removed and the Light theme) |
| `https://basmia.hmtech.solutions/login` | Login page showing "Basmia" as the gym name above the form |
| `https://hmtech.solutions/login` | Login page showing the "Please use your gym's URL" warning |
| `https://basmia.hmtech.solutions/api/sync` (with valid `X-Api-Key`) | Sync API works (soft migration — root URL also still works) |
| `https://nonexistent.hmtech.solutions/login` | Falls through to the root-domain login (no matching gym in DB) |

## Local development testing (optional, before VPS deploy)

To test the subdomain flow on your dev machine without DNS:

1. Edit `C:\Windows\System32\drivers\etc\hosts` (Administrator):
   ```
   127.0.0.1 basmia.localhost
   127.0.0.1 admin.localhost
   ```
2. Run the web project locally (`dotnet run` from `src/AccessControlPro.Web/`).
3. Open browser to `http://basmia.localhost:5000/login` — middleware should
   resolve `basmia` and the login page should show Basmia's name.

If you don't have a `basmia` row in your local master DB yet, run the
backfill SQL against your local PostgreSQL the same way.

## Rollback plan

If anything goes wrong:

1. Revert the Nginx config (the previous version is in `/etc/nginx/sites-available/gymapp.backup` after step 3 if you `cp` first).
2. Restart Nginx and the app: `systemctl reload nginx && systemctl restart gymapp`.
3. The subdomain feature is non-destructive — even with the new code deployed,
   if there's no matching Subdomain row the request falls through to the
   root-domain login flow (legacy behaviour).
