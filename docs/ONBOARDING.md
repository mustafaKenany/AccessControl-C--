# New Customer Onboarding Checklist

End-to-end procedure for installing AccessControlPro at a new gym. Total time on-site: about 45 minutes.

## Before you arrive

- [ ] Customer's PC meets minimum specs: **i5 (or newer) + 8 GB RAM + SSD + Windows 10/11**
- [ ] SQL Server 2019 or 2022 installed on the customer's PC (Express edition is fine for small gyms)
- [ ] Access control device(s) installed and on the customer's network (note IPs)
- [ ] Internet connection at the customer's site (for cloud sync)
- [ ] Customer's gym name decided (will become their subdomain — e.g. "gymfit" → `gymfit.hmtech.solutions`)
- [ ] Reserved gym entry on cloud (SuperAdmin → Gyms → New) with their subdomain set
- [ ] Latest `publish/customer-deploy/main-app/` build copied to a USB stick
- [ ] `tools/` folder copied to the same USB stick (tuneup + cleanup scripts)

## At the customer's site — step by step

### 1. Copy files (5 min)
- [ ] Copy `main-app/` contents to `C:\AccessControlPro\` (or customer's preferred location)
- [ ] Copy `tools/` to the customer's Desktop

### 2. First launch (2 min)
- [ ] Run `AccessControlPro.WPF.exe`
- [ ] Setup wizard appears
- [ ] Fill in: DB connection (`localhost / sa / <password>`), gym name, device IP(s), backup path
- [ ] **Leave the `CloudApiKey` field blank** — the wizard auto-generates a unique one per install. Copy the generated key to your records.
- [ ] Click Finish — wizard runs DB migration which auto-seeds 5 default subscription plans

### 3. Post-install setup (3 min)
- [ ] Right-click `tools/post-install/post-install.bat` → **Run as administrator**
- [ ] Press Y
- [ ] Wait until "Post-install setup complete" message
- [ ] **Log out and back in** (Windows visual-effects change needs a fresh session)

### 4. Adjust default subscription plans (5 min)
- [ ] Open admin panel (separate app — `AccessControlPro.Admin.exe`)
- [ ] Navigate to Subscription Plans
- [ ] Adjust the 5 pre-seeded plans (Fitness, Cardio, CrossFit, Full Access, Daily Pass) to match the customer's actual pricing
- [ ] Save

### 5. Cloud-side configuration (5 min)
SSH into the VPS or use SuperAdmin to:
- [ ] Confirm the gym row in the master `Gyms` table has the correct `Subdomain` and the auto-generated `CloudApiKey` from step 2
- [ ] Run the QR seed script if web QR is needed: `tools/qr-test/check-and-seed-cloud-qr.sql` against the gym's cloud DB
- [ ] Verify `https://<subdomain>.hmtech.solutions/login` shows the gym name

### 6. First sync (3 min)
- [ ] In the WPF app, wait 5 minutes for the first auto-sync
- [ ] On the cloud, go to `https://admin.hmtech.solutions/superadmin/gym-health` — the new gym should appear with status "Healthy" or "Watch" (depending on data volume)
- [ ] Go to `/superadmin/data-quality` — confirm "Plans" column is green (not zero)

### 7. Send diagnostics test (3 min)
- [ ] In the WPF sidebar, click **Send Diagnostics**
- [ ] Type "post-install test" in the note
- [ ] Click Send
- [ ] On the cloud, go to `/superadmin/diagnostics` — confirm the bundle appears with the gym name

### 8. Train the receptionist (10 min)
- [ ] Show them how to add a player + assign a card
- [ ] Show them how to renew a subscription
- [ ] Show them where the "Send Diagnostics" button is
- [ ] Tell them: **"If you see a popup after 5 days asking to restart the app, please click yes — it helps performance."**
- [ ] Tell them: **"Card numbers work with or without leading zeros — both 0366549 and 366549 will find the same card."**

### 9. Handover (5 min)
- [ ] Save a copy of the auto-generated `CloudApiKey` in your customer records
- [ ] Save a copy of the customer's DB password (set during setup wizard)
- [ ] Give the customer a one-pager with:
  - Support phone/email
  - "How to restart the app" instructions
  - "How to send a diagnostics bundle" instructions

## Post-install — your monitoring (next 7 days)

- [ ] **Day +1**: Check `/superadmin/gym-health` — should be Green or Yellow
- [ ] **Day +1**: Check `/superadmin/diagnostics` for the post-install bundle
- [ ] **Day +3**: Check `memory_log.txt` from the diagnostics bundle — working set should stay under 400 MB
- [ ] **Day +7**: Check `/superadmin/diagnostics` — auto-uploaded bundle from the 15-day timer should be there
- [ ] **Day +14**: Confirm at least one renewal happened in the cloud (gym is actually using the app)

## What to do if a gym goes Red on the Gym Health dashboard

1. **Last sync > 24h ago** → call the gym; usually their PC is off or internet is down
2. **2+ crashes in last 14d** → check the latest crash-recovery diagnostics bundle for the stack trace
3. **No diagnostics ever uploaded** → check that their `CloudApiKey` is correct in their `appsettings.json`

## Things that go wrong sometimes

| Symptom | Cause | Fix |
|---|---|---|
| Wizard fails on DB connect | SQL Server not running | `net start MSSQLSERVER` |
| First sync fails with 401 | API key mismatch between local and cloud | Compare `appsettings.json` `CloudApiKey` with cloud's `Gyms.ApiKey` row |
| Subdomain login shows "Please use your gym's URL" | Subdomain not set on `Gyms` row | UPDATE the row with the correct `Subdomain` |
| "No QR codes available" on cloud | Cloud-source QR pool not seeded | Run `tools/qr-test/check-and-seed-cloud-qr.sql` on the VPS |
| Tuneup.bat says "sqlcmd not found" | SQL Server Command Line Utilities not installed | Install from Microsoft (6 MB download) or run the SQL manually via SSMS |
