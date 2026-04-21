# AccessControlPro - Complete Testing Guide

## Quick Smoke Test (15 minutes)

Run these 10 tests to verify the app works before deploying to a customer:

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | App starts | Double-click exe | Login screen appears, no crash |
| 2 | Login | Username: admin, Password: (your pass) | Dashboard loads with stats |
| 3 | Add Player | Players > Add > Name: "Test User" / Card: "12345" / Phone: "07701234567" / Fee: 50000 / Paid: 50000 / End: +30 days | Player appears in list |
| 4 | Search Player | Type "Test" in search box | "Test User" appears |
| 5 | Device Search | Devices > Search Network | Device found (or "No device" if none connected) |
| 6 | Assign Card | Select player > Assign Card > Card: "12345" > Select device | Card assigned, sync to device |
| 7 | Monitor Start | Monitor > Start | "Monitoring 1 device(s)" shown |
| 8 | Card Swipe | Swipe card "12345" at device | Event appears in live feed with "Active" status |
| 9 | Cloud Sync | Wait 5 min (or trigger manually) | Player appears on cloud portal |
| 10 | App Exit | Close app | Clean exit, no hanging process |

---

## Full Testing Guide

### MODULE 1: PLAYERS

#### Test 1.1: Add Player (Valid)
```
Input:
  Name EN: "Ahmed Ali"
  Name AR: "احمد علي"
  Card No: "100001"
  Phone: "07701000001"
  Subscription: "Monthly"
  Fee: 50000
  Paid: 50000
  Start: today
  End: today + 30 days
  Max Visits: 0 (unlimited)

Expected: Player created, appears in list, transaction auto-created
```

#### Test 1.2: Add Player (Visit-Based)
```
Input:
  Name EN: "Sara Hassan"
  Card No: "100002"
  Phone: "07701000002"
  Fee: 30000
  Paid: 30000
  Max Visits: 20

Expected: Player created with "0/20" visits shown
```

#### Test 1.3: Add Player - Duplicate Card (ERROR)
```
Input:
  Name: "Duplicate Test"
  Card No: "100001"  (same as Test 1.1)
  Phone: "07701000099"

Expected ERROR: "Card number '100001' is already assigned to another player"
```

#### Test 1.4: Add Player - Duplicate Phone (ERROR)
```
Input:
  Name: "Duplicate Phone"
  Card No: "999999"
  Phone: "07701000001"  (same as Test 1.1)

Expected ERROR: "Phone number already used by another player"
```

#### Test 1.5: Add Player - Missing Name (ERROR)
```
Input:
  Name EN: ""
  Name AR: ""
  Card No: "100003"

Expected ERROR: "At least one name (English or Arabic) is required"
```

#### Test 1.6: Add Player - End Before Start (ERROR)
```
Input:
  Start: 2026-05-01
  End: 2026-04-01

Expected ERROR: "End date must be after start date"
```

#### Test 1.7: Edit Player
```
Steps:
  1. Select "Ahmed Ali" > Edit
  2. Change Phone to "07701000099"
  3. Enter reason: "Phone number updated"
  4. Save

Expected: Phone updated, audit log records change
```

#### Test 1.8: Freeze Player
```
Steps:
  1. Select "Ahmed Ali" > Freeze
  2. Reason: "Travel abroad"
  3. Confirm

Expected: Player shows "Frozen" badge, card swipes denied
```

#### Test 1.9: Unfreeze Player
```
Steps:
  1. Select frozen player > Unfreeze
  2. Confirm

Expected: EndDate extended by freeze duration, access restored
```

#### Test 1.10: Renew Subscription
```
Steps:
  1. Select player > Renew
  2. New End Date: +30 days
  3. Fee: 50000, Paid: 50000

Expected: EndDate updated, UsedVisits reset to 0, transaction created
```

#### Test 1.11: Delete Player
```
Steps:
  1. Select player > Delete
  2. Reason: "Cancelled membership"
  3. Confirm

Expected: Player moved to Deleted Records
```

#### Test 1.12: Delete Player with Transactions (ERROR)
```
Steps:
  1. Select player who has payments > Delete

Expected ERROR: "Cannot delete player: This player has X associated transaction records"
```

#### Test 1.13: Visit Count Limit
```
Setup: Create player with MaxVisits=3
Steps:
  1. Swipe card 3 times (all should succeed with "Visit 1/3", "2/3", "3/3")
  2. Swipe card 4th time

Expected: 4th swipe DENIED with "Visit limit reached (3/3)"
Important: UsedVisits should be exactly 3, NOT 4
```

#### Test 1.14: Subscription Expiry - Last Day
```
Setup: Create player with EndDate = TODAY
Steps:
  1. Swipe card

Expected: Access ALLOWED (player can use gym on last day)
Next day: Access DENIED with "Subscription expired"
```

---

### MODULE 2: DEVICES

#### Test 2.1: Search Network
```
Steps:
  1. Connect FCard device to same network
  2. Devices > Search Network

Expected: Device found with IP, Serial Number, MAC
```

#### Test 2.2: Add Device
```
Steps:
  1. Search finds device
  2. Click Add

Expected: Device added to list, doors auto-created (1/2/4 based on model)
```

#### Test 2.3: WiFi Warning
```
Steps:
  1. Connect WiFi to same subnet as Ethernet
  2. Try to search/connect

Expected WARNING: "WiFi and Ethernet are on the same subnet"
```

#### Test 2.4: Device Offline
```
Steps:
  1. Disconnect device from network
  2. Try any operation (open door, sync cards)

Expected ERROR: "Device is not reachable at {IP}"
```

#### Test 2.5: Remote Open Door
```
Steps:
  1. Select online device
  2. Click "Open Door"
  3. Select door if multiple

Expected: Physical door unlocks, success message
```

#### Test 2.6: Factory Reset
```
Steps:
  1. Select device > Factory Reset
  2. Confirm first warning
  3. Confirm FINAL warning

Expected: All cards/logs erased from device, cards need re-sync
```

#### Test 2.7: Download Logs
```
Steps:
  1. Select device > Download Logs
  2. Choose "1 Month"

Expected: "Downloaded X records from {device}" with count
```

#### Test 2.8: Sync All Players
```
Steps:
  1. Select device > Sync All Players
  2. Confirm

Expected: Progress shows "X/Y - CardNumber", final count summary
```

---

### MODULE 3: MONITORING

#### Test 3.1: Start Monitor
```
Steps:
  1. Monitor tab > Start

Expected: "Monitoring X device(s)..." status shown
```

#### Test 3.2: Card Swipe - Active Player
```
Steps:
  1. Swipe valid card at device

Expected Event:
  Direction: Entry (or Exit)
  Card Status: Active
  Player Name: "Ahmed Ali | احمد علي"
  Event: "Card Open"
```

#### Test 3.3: Card Swipe - Frozen Player
```
Steps:
  1. Freeze a player
  2. Swipe their card

Expected Event:
  Card Status: Frozen
  Access: DENIED
```

#### Test 3.4: Card Swipe - Expired Player
```
Steps:
  1. Create player with EndDate = yesterday
  2. Swipe card

Expected Event:
  Card Status: Expired
  Access: DENIED
```

#### Test 3.5: Card Swipe - Unregistered Card
```
Steps:
  1. Swipe a card not in the system

Expected Event:
  Card Status: Not Registered
  Player Name: empty
```

#### Test 3.6: QR Code Swipe
```
Steps:
  1. Create QR Pass
  2. Scan QR at device

Expected Event:
  Card Status: Active
  Player Name: "QR Guest | ضيف QR"
  Card Number: starts with "5000"
```

#### Test 3.7: Projector Display
```
Steps:
  1. Click "Projector Display" during monitoring
  2. Select doors
  3. Swipe card

Expected: Large display shows player name, photo, direction, status
```

#### Test 3.8: Monitor Lock (Multi-PC)
```
Steps:
  1. Start monitor on PC1
  2. Try to start monitor on PC2

Expected ERROR on PC2: "Real-Time Monitor is already running on: PC1-NAME"
```

---

### MODULE 4: FINANCE

#### Test 4.1: View Finance Dashboard
```
Steps:
  1. Finance tab
  2. Select "This Month"

Expected: Revenue, Expenses, Net Profit calculated correctly
```

#### Test 4.2: Pay Outstanding Balance
```
Setup: Player with Fee=50000, Paid=30000 (owes 20000)
Steps:
  1. Finance > Outstanding > Select player
  2. Pay 20000

Expected: Balance cleared, transaction created
```

#### Test 4.3: Filter by Period
```
Steps:
  1. Select "Today" filter
  2. Verify only today's transactions shown
  3. Select "This Week" filter (starts Saturday)
  4. Verify week range correct

Expected: Correct date ranges, Saturday-based week
```

---

### MODULE 5: POS

#### Test 5.1: Open Shift
```
Steps:
  1. POS tab > Open Shift
  2. Opening Cash: 100000

Expected: Shift opened, status shows "Open by {user}"
```

#### Test 5.2: Sell Product (Cash)
```
Steps:
  1. Add "Water Bottle" x2 to cart
  2. Payment: Cash
  3. Complete Sale

Expected: Transaction created, stock decremented by 2
```

#### Test 5.3: Sell Product (Card Balance)
```
Setup: Player with CardBalance = 50000
Steps:
  1. Select player by name/card
  2. Add product (10000 IQD)
  3. Payment: Card Balance

Expected: Player CardBalance reduced by 10000, transaction created
```

#### Test 5.4: Insufficient Stock (ERROR)
```
Setup: Product with Stock = 1
Steps:
  1. Add product x3 to cart
  2. Complete Sale

Expected ERROR: "Insufficient stock for 'Product'. Available: 1, Requested: 3"
Stock should NOT be deducted (atomic operation)
```

#### Test 5.5: Apply Discount
```
Steps:
  1. Add products totaling 50000
  2. Apply 10% discount, reason: "Loyal customer"

Expected: Total = 45000, discount shown, reason recorded
```

#### Test 5.6: Close Shift
```
Steps:
  1. Close Shift
  2. Closing Cash: 120000

Expected: Summary shows sales total, variance (if any), shift closed
```

---

### MODULE 6: QR PASS

#### Test 6.1: Create QR Pass
```
Steps:
  1. QR tab > Create Pass
  2. Name: "Guest Ali"
  3. Phone: "07701999999"
  4. Fee: 5000
  5. Max Uses: 2
  6. Valid: 1 day

Expected: QR code generated and displayed
```

#### Test 6.2: Use QR Pass
```
Steps:
  1. Scan QR at device (use card number from pool)
  2. Scan again (2nd use)
  3. Scan 3rd time

Expected:
  1st: "Access granted. 1 uses remaining" 
  2nd: "Access granted. 0 uses remaining" (auto-deactivates)
  3rd: DENIED "QR pass has reached maximum uses"
```

#### Test 6.3: Expired QR Pass
```
Setup: QR pass with ValidTo = yesterday
Steps:
  1. Scan QR

Expected DENIED: "QR pass has expired"
```

---

### MODULE 7: CLOUD PORTAL

#### Test 7.1: Owner Login
```
Steps:
  1. Go to https://hmtech.solutions/login
  2. Username/Password from local Users table

Expected: Dashboard loads with gym data
```

#### Test 7.2: Sync Verification
```
Steps:
  1. Add player locally
  2. Wait 5 min (or trigger sync)
  3. Check cloud Players page

Expected: New player appears on cloud
```

#### Test 7.3: Delete Sync (0 Players)
```
Steps:
  1. Delete ALL players locally
  2. Wait for sync
  3. Check cloud

Expected: Cloud shows 0 players (table cleared)
```

#### Test 7.4: Language Toggle
```
Steps:
  1. Login to cloud
  2. Click language toggle (EN/عربي)

Expected: All text switches language, RTL/LTR changes
Important: OTHER users should NOT be affected (per-session language)
```

#### Test 7.5: Multi-User Language
```
Steps:
  1. Open browser 1 > login > set Arabic
  2. Open browser 2 (incognito) > login > set English

Expected: Each browser shows its own language independently
```

#### Test 7.6: Session Timeout
```
Steps:
  1. Login
  2. Wait 24+ hours
  3. Refresh page

Expected: Redirected to login page (session expired)
```

#### Test 7.7: Player Login (Cloud)
```
Steps:
  1. Go to /login
  2. Switch to "Player" tab
  3. Phone: player's phone
  4. Last 4 digits of card number

Expected: Player dashboard shows subscription info, visits, payments
```

#### Test 7.8: Super Admin Login
```
Steps:
  1. Go to /superadmin
  2. Username: superadmin
  3. Password: HMTech2026!

Expected: Super Admin dashboard with gym list
```

#### Test 7.9: Cloud Filters
```
Steps:
  1. Events page > hover each filter button

Expected: Tooltip shows date range (e.g., "This Week: Sat Apr 4 - Fri Apr 10")
Both EN and AR descriptions shown
```

---

### MODULE 8: PERMISSIONS

#### Test 8.1: Limited User
```
Setup: Create user with ONLY "PlayersView" permission
Steps:
  1. Login as limited user
  2. Try to add player

Expected: "Add" button hidden/disabled
```

#### Test 8.2: No Monitor Permission
```
Setup: User without "MonitorView" permission
Steps:
  1. Login > go to Monitor tab

Expected: Monitor tab hidden or start button disabled
```

---

### MODULE 9: BILINGUAL

#### Test 9.1: Switch to Arabic
```
Steps:
  1. Click language toggle to Arabic

Expected: All UI text in Arabic, RTL layout, Cairo font
```

#### Test 9.2: Arabic Player Names
```
Steps:
  1. Add player with Arabic name
  2. View in players list
  3. View in events after swipe
  4. View on cloud portal

Expected: Arabic name displayed correctly everywhere
```

---

### MODULE 10: BACKUP & RECOVERY

#### Test 10.1: Manual Backup
```
Steps:
  1. Admin > Backup > Create Backup

Expected: .bak file created in Backups/ folder with timestamp
```

#### Test 10.2: Auto Backup
```
Steps:
  1. Wait for 2AM or 2PM

Expected: Backup auto-created without user action
```

---

## Edge Case Tests

| # | Test | Input | Expected |
|---|------|-------|----------|
| E1 | Empty gym (no players) | Sync to cloud | Cloud shows empty tables |
| E2 | 0 fee subscription | Fee: 0, Paid: 0 | Player created, no transaction |
| E3 | Very long name | 200 chars | Truncated or accepted |
| E4 | Special chars in name | "O'Brien & Son's" | Handled without SQL error |
| E5 | Arabic-only name | EN: "", AR: "احمد" | Accepted, EN auto-copied |
| E6 | Max visits = 1 | Single visit subscription | Works after 1 swipe, denied on 2nd |
| E7 | Renew expired player | Player expired 30 days ago | Renewal works, new dates set |
| E8 | Freeze then delete | Freeze > try delete | Should work (or show clear error) |
| E9 | 2 cards same player | Assign 2nd card | Both cards work for same player |
| E10 | Network disconnect during sync | Pull ethernet during cloud sync | Error logged, retry on next cycle |

---

## AI/Automated Testing Tools

### For Desktop WPF Testing (Button Clicks, UI Automation):

1. **Microsoft Coded UI Test / Appium for Windows**
   - Free, official Microsoft tool
   - Records button clicks, text input, tab navigation
   - Generates C# test code automatically
   - Works with WPF apps
   - URL: https://github.com/microsoft/WinAppDriver

2. **FlaUI**
   - Free, open-source WPF/WinForms automation
   - Modern alternative to Coded UI
   - C# API: `var button = window.FindFirstDescendant(cf => cf.ByName("Add Player")); button.Click();`
   - URL: https://github.com/FlaUI/FlaUI

3. **TestComplete (SmartBear)**
   - Commercial ($$$) but very powerful
   - AI-powered object recognition
   - Record & playback mode
   - Visual test editor
   - Supports WPF, WinForms, Web
   - 30-day free trial

4. **Ranorex**
   - Commercial, enterprise-grade
   - AI-based element identification
   - Record clicks/inputs, generates tests
   - Export to C# or standalone
   - Good for WPF

### For Web/Cloud Portal Testing (Blazor):

5. **Playwright (Microsoft)**
   - FREE, best modern web testing tool
   - Records browser actions into code
   - `npx playwright codegen https://hmtech.solutions`
   - Generates C#, JS, or Python test scripts
   - Handles Blazor SSR + Interactive perfectly
   - URL: https://playwright.dev

6. **Selenium**
   - FREE, classic web automation
   - C# bindings available (Selenium.WebDriver NuGet)
   - Works with all browsers

7. **Cypress**
   - FREE, JavaScript-based
   - Visual test runner
   - Good for Blazor apps

### AI-Powered Testing Tools (New):

8. **Testim.io**
   - AI auto-heals broken tests when UI changes
   - Record & playback with smart element detection
   - Cloud-based execution

9. **Mabl**
   - AI-powered end-to-end testing
   - Auto-generates tests by exploring your app
   - Visual regression detection

10. **Katalon Studio**
    - FREE community edition
    - AI-powered test generation
    - Record browser/desktop actions
    - Export to multiple formats

### Recommended Setup for Your Project:

**For Local WPF App:**
```
FlaUI (free) + NUnit test framework
- Record once, replay forever
- Integrate with CI/CD
```

**For Cloud Portal:**
```
Playwright (free) + record mode
- Run: npx playwright codegen https://hmtech.solutions
- Click through the app, it generates test code
- Save and replay anytime
```

**Simplest Start (5 minutes):**
```bash
# Install Playwright
dotnet new nunit -n AccessControlPro.Tests
cd AccessControlPro.Tests
dotnet add package Microsoft.Playwright
dotnet add package Microsoft.Playwright.NUnit
dotnet build
pwsh bin/Debug/net8.0/playwright.ps1 install

# Record a test (opens browser, records your clicks)
npx playwright codegen https://hmtech.solutions
```
