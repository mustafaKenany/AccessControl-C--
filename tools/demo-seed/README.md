# Demo Data Seeder — for screenshots and demos

This folder contains a SQL script that fills a fresh database with realistic-looking
demo data so you can take professional screenshots of the app.

## ⚠️ Safety first

**ONLY run this on a new demo database. Do NOT run it on the customer's production data.**
The script uses `SET IDENTITY_INSERT` and inserts specific IDs — if you ran it against
a populated database, you'd get conflicts and data corruption.

## Setup steps

### 1. Create a fresh database

Open SQL Server Management Studio (SSMS), connect to your local SQL Server, then:

```sql
CREATE DATABASE AccessControlPro_Demo;
```

### 2. Make the app create the schema

Run the WPF app once and point the setup wizard at this new database:

```
d:\AccessControlPro\publish\customer-deploy\main-app\AccessControlPro.WPF.exe
```

In the setup wizard:
- **Server:** `localhost`
- **Database:** `AccessControlPro_Demo`
- **User:** `sa`
- **Password:** *your local sa password*
- Click **Test** → should succeed → **Save**

The app will run the schema migrations automatically (creates all tables).

Then **close the app**.

### 3. Run the demo seed script

In SSMS:
1. Open `demo-data.sql` (this folder)
2. Make sure `AccessControlPro_Demo` is selected as the database (top-left dropdown)
3. Press **F5** (Execute)
4. You should see:
   ```
   ✅ Demo data seeded!
      - 12 players (mix of active/expiring/expired/frozen)
      - 12 access cards
      - 30 recent access events
      - 11 transactions (subscriptions + POS sales + expense)
      - 5 subscription plans
      - 7 products
   ```

### 4. Reopen the app

Run the app again. You'll see:
- 12 players in the Players list — mix of states for variety
- Recent activity in the Events screen
- Stats on the Dashboard
- Transaction history in Finance
- POS products ready to sell

## What's in the demo data

### Players (12 total)
| Name | State | Why for screenshots |
|---|---|---|
| Ahmad Al-Hassan | Active monthly | Normal active member |
| Sarah Mohammed | Quarterly half-paid | Shows "Remaining" balance feature |
| Omar Khalid | Expiring in 5 days | Shows "Expiring Soon" stat |
| Fatima Ali | **Frozen** (vacation) | Shows freeze feature |
| Yusuf Ibrahim | Expired 3 days ago | Shows "Expired" stat + renewal flow |
| Maryam Hussein | Quarterly fresh | Shows long-term subscription |
| Hassan Mahmoud | Active monthly | Normal active member |
| Nour El-Din | Joined today | Shows "new today" |
| Reem Awad | Expiring tomorrow | Urgent renewal scenario |
| Khalid Saleh | Quarterly | Mid-term member |
| Aisha Rahman | Active monthly | Normal active member |
| Tariq Nassar | **Frozen** (medical) | Second freeze for variety |

### Devices & Doors
- 2 access controllers (Main + Side)
- 3 doors (Front, Back, Side)

### Events
30 recent card swipes spread across the last 24 hours, including:
- Most as successful entries
- 1 denied (expired card)
- 1 denied (frozen card)
- Mix of doors

### Transactions
- 5 subscription payments (today + earlier this week)
- 4 POS sales (water, protein shake, energy drink, towel)
- 1 expense (cleaning supplies)

### Subscription Plans
5 typical gym plans: Monthly, Quarterly, Half-Year, Annual, 10-Visit Pass

### Products (POS)
7 typical gym products with realistic prices

## Tips for screenshots

1. **Maximize the window** to a clean ratio (1920×1080 looks best)
2. **Use Win+Shift+S** → Window snip mode for clean captures
3. **Toggle language** — capture some screens in Arabic too (RTL is impressive)
4. **Add a photo** to one or two players manually before screenshotting (gives the player profile screen depth)

## Cleanup after screenshots

When done, just drop the demo DB:

```sql
USE master;
DROP DATABASE AccessControlPro_Demo;
```

Your real database is untouched.

## Re-running the script

The script is **NOT** idempotent (no DELETE/TRUNCATE at the start, on purpose for safety).
If you want to re-seed:
1. Drop and recreate the demo DB
2. Re-run setup wizard
3. Re-run the script
