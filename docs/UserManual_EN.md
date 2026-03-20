# AccessControlPro - User Manual

**Version 3.4 | Gym & Fitness Club Management System**
**with Access Control, POS, and Financial Management**

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Installation & Setup](#2-installation--setup)
3. [Login & Authentication](#3-login--authentication)
4. [Main Application (WPF)](#4-main-application)
   - [Dashboard](#41-dashboard)
   - [Device Management](#42-device-management)
   - [Door Management](#43-door-management)
   - [Player Management](#44-player-management)
   - [Access Events](#45-access-events)
   - [Finance](#46-finance)
   - [Cash Flow](#47-cash-flow)
   - [QR Passes](#48-qr-passes)
   - [Monitor Display](#49-monitor-display)
   - [Logs & Audit](#410-logs--audit)
   - [Deleted Records](#411-deleted-records)
   - [Data Migration](#412-data-migration)
5. [Admin Panel](#5-admin-panel)
   - [User Management](#51-user-management)
   - [Categories & Products](#52-categories--products)
   - [Suppliers & Purchase Orders](#53-suppliers--purchase-orders)
   - [Reports](#54-reports)
   - [Audit Log](#55-audit-log)
   - [Backup & Restore](#56-backup--restore)
   - [Settings](#57-settings)
6. [POS Terminal](#6-pos-terminal)
   - [Selling Products](#61-selling-products)
   - [Card Balance & Top-Up](#62-card-balance--top-up)
   - [Barcode Scanner](#63-barcode-scanner)
7. [License Activation](#7-license-activation)
8. [Security Features](#8-security-features)
9. [Troubleshooting](#9-troubleshooting)

---

## 1. System Overview

AccessControlPro is a comprehensive gym management system that includes:

- **Main App (WPF)** - Daily gym operations: player management, access control, events, finance
- **Admin Panel** - System administration: users, products, suppliers, reports, backup
- **POS Terminal** - Point-of-sale: product sales, card balance, barcode scanning

### System Requirements

| Component | Requirement |
|-----------|-------------|
| Operating System | Windows 10 / 11 (64-bit) |
| Database | SQL Server 2019+ or SQL Server Express |
| RAM | Minimum 4 GB |
| Storage | 500 MB for application + database |
| .NET Runtime | Not required (self-contained) |
| Access Control Hardware | FK-compatible controllers (optional) |

### Key Features

- Player subscription management with card-based access control
- Real-time device monitoring and event logging
- QR code passes for daily/temporary visitors
- Point-of-sale with inventory management
- Financial tracking (income, expenses, outstanding balances)
- Bilingual interface (English / Arabic)
- Role-based user permissions
- Data encryption and security hardening
- Multi-monitor support for entrance display

---

## 2. Installation & Setup

### First-Time Installation

1. Copy the **WPF** folder to the target PC (e.g., `C:\AccessControlPro`)
2. Run `AccessControlPro.WPF.exe`
3. The **Setup Wizard** will launch automatically on first run

### Setup Wizard Steps

#### Step 1: Welcome
- Welcome screen with application information
- Developer logo upload (optional)

#### Step 2: Database Connection
- **Server**: Enter SQL Server name (e.g., `localhost`, `.\SQLEXPRESS`, or remote IP)
- **Database**: Name for the database (default: `AccessControlPro`)
- **Authentication**: SQL Server credentials (User ID + Password) or Windows Authentication
- Click **Test Connection** to verify before proceeding

#### Step 3: Gym Information
- **Gym Name**: Your gym/club name (appears in reports and receipts)
- **Currency**: Currency symbol for financial displays (e.g., IQD, USD, EUR)
- Gym logo upload (optional, displayed in reports)

#### Step 4: Admin Account
- **Username**: Administrator login name
- **Password**: Must meet password policy (minimum 8 characters, mixed case, number)
- **Email**: Admin email for recovery

#### Step 5: Finish
- Review configuration summary
- Option to create desktop shortcuts for all 3 apps
- Click **Finish** to complete setup

### Installing Additional Apps

For the **Admin Panel** and **POS Terminal**:
1. Copy the `Admin` and/or `POS` folders to the same PC or other PCs
2. Edit `appsettings.json` in each folder to match the same database connection string
3. Run the respective `.exe` file

---

## 3. Login & Authentication

### Logging In
1. Launch the application
2. Enter your **Username** and **Password**
3. Click **Login** or press **Enter**
4. Check **Remember Me** to save your username for next login

### Security Protections
- **Brute force protection**: Account locks after 5 failed attempts (15-minute cooldown)
- **Password policy**: Minimum 8 characters with complexity requirements
- **Session tracking**: All actions are logged with the current user

### Changing Password
1. Click the **key icon** in the bottom sidebar
2. Enter your current password
3. Enter and confirm your new password
4. Click **Change Password**

### Language Toggle
- Click the **globe icon** in the bottom sidebar to switch between English and Arabic
- The interface updates immediately without restart

---

## 4. Main Application

### 4.1 Dashboard

The dashboard provides a real-time overview of your gym operations:

| Card | Description |
|------|-------------|
| **Total Devices** | Number of access control devices registered |
| **Online Devices** | Devices currently connected and responding |
| **Total Doors** | Number of doors configured across all devices |
| **Today's Events** | Access events recorded today |
| **Active Alarms** | Current alarm conditions |

- **Recent Events**: Shows the last 20 access events with door name, card number, event type, and timestamp
- **Door Status**: Shows all doors with their current lock/unlock state
- Click **Refresh** to update all statistics

### 4.2 Device Management

Manage your access control hardware devices.

#### Adding a Device
1. Click **Add Device**
2. Enter device details:
   - **Name**: Friendly name (e.g., "Main Entrance")
   - **IP Address**: Device network IP
   - **Port**: Communication port (default: 8000)
   - **Username/Password**: Device credentials
3. Click **Save**

#### Network Discovery
1. Click **Search Network** to auto-discover devices on your local network
2. Discovered devices will appear in the list
3. Select and add devices to your system

#### Connecting to a Device
1. Select a device from the list
2. Click **Connect** to establish communication
3. The status indicator will show green (online) or red (offline)

#### Downloading Event Logs
1. Select an online device
2. Click **Download Logs**
3. The system verifies device connectivity first
4. Select the date period for log download
5. Events are downloaded and saved to the database
6. A summary dialog shows the number of records imported

### 4.3 Door Management

Configure doors linked to your access control devices.

#### Adding a Door
1. Click **Add Door**
2. Select the **parent device**
3. Enter door details:
   - **Name**: Door name (e.g., "Front Door", "VIP Room")
   - **Door Number**: Physical door number on the device (1-4)
4. Click **Save**

### 4.4 Player Management

The core of daily operations - managing gym members and their access cards.

#### Adding a New Player
1. Click **Add Player**
2. Fill in player details:
   - **Full Name (EN)**: English name
   - **Full Name (AR)**: Arabic name
   - **Phone**: Contact number
   - **Card No**: Member card/ID number
   - **Subscription Type**: Select from configured plans
   - **Duration**: Months or custom days
   - **Fee / Amount Paid**: Subscription cost and payment received
   - **Photo**: Upload player photo (optional)
3. Click **Save**

#### Assigning an Access Card
1. Select a player from the list
2. Click **Assign Card**
3. Enter card details:
   - **Card Number**: Physical card number
   - **Door Permissions**: Select which doors the card can access
   - **Effective Times**: Access schedule
4. Select **target devices** to sync the card to
5. The system checks device connectivity before syncing
6. Click **Assign** to save and sync

#### Renewing a Subscription
1. Select a player
2. Click **Renew**
3. Choose:
   - **Subscription Type**: New or same plan
   - **Duration**: Months or custom days
   - **Fee / Amount Paid**
   - **Door Permissions**: Update if needed
4. Select devices to sync updated card validity
5. Click **Renew**
6. A receipt is generated for the transaction

#### Freezing a Player
1. Select a player
2. Click **Freeze**
3. Enter a **freeze reason** (e.g., "Travel", "Medical")
4. The player's subscription is paused
5. Cards are disabled on hardware devices
6. When unfreezing, the remaining subscription days are automatically added back

#### Unfreezing a Player
1. Select a frozen player
2. Click **Unfreeze**
3. Confirm the action
4. The subscription end date is extended by the freeze duration
5. Cards are re-enabled on hardware devices

#### Bulk Operations
1. Click **Bulk Operations**
2. Select operation type:
   - **Freeze All**: Freeze multiple players at once
   - **Unfreeze All**: Unfreeze multiple players
   - **Extend Subscription**: Add days to multiple players
3. Select target group:
   - All active players
   - Expiring in 7 days
   - Expiring in 30 days
   - Currently filtered list
4. Confirm and execute

#### Filtering Players
Use the filter chips at the top:
- **All**: Show all players
- **Expiring**: Players expiring within 7 days
- **Renewed**: Recently renewed players
- **Frozen**: Currently frozen players
- **Expired**: Past-due subscriptions

Use the **search bar** to find players by name, card number, phone, or subscription type.

### 4.5 Access Events

View and manage access events from all devices.

#### Viewing Events
- Events show: timestamp, door name, card number, event type, description
- Use **pagination** to browse through large event lists (100 per page)

#### Filtering Events
- **By Type**: Card action, Button, Door sensor, Software, Alarm, System
- **By Device**: Select specific device
- **By Period**: Today, Yesterday, This week, Last week, This month, Last month, Last 3/6 months, This year, or Custom range

#### Downloading Device Logs
1. Select a device from the dropdown
2. Click **Download Logs**
3. Select date period
4. The system downloads events directly from the device hardware

### 4.6 Finance

Track your gym's financial health.

#### Overview
- **Total Revenue**: All income in the selected period
- **Total Expenses**: All expenses in the selected period
- **Net Profit**: Revenue minus expenses
- **Unpaid Balances**: Outstanding subscription payments

#### Recent Transactions
- Shows the most recent transactions with type, category, amount, and date
- Filter by period (Today, This week, This month, etc.)
- Search by description or player name

#### Outstanding Balances
- Lists players who have unpaid subscription balances
- Shows: Player name, subscription fee, amount paid, remaining balance
- Click **Pay** to record a partial or full payment

### 4.7 Cash Flow

Manage daily income and expense transactions.

#### Adding Income
1. Click **Add Income**
2. Select **category** (Subscription, POS Sales, Owner Deposit, Other)
3. Enter **amount** and **description**
4. Select **payment method**
5. Click **Save**

#### Adding Expense
1. Click **Add Expense**
2. Select **category** (Rent, Electricity, Water, Salaries, Equipment, etc.)
3. Enter **amount** and **description**
4. Click **Save**

### 4.8 QR Passes

Create temporary QR code passes for daily visitors.

#### Creating a QR Pass
1. Click **Create Pass**
2. Enter visitor details:
   - **Player Name**: Visitor name
   - **Phone**: Contact number
   - **Fee**: Pass fee amount
   - **Max Uses**: Maximum number of entries (default: 5)
   - **Valid Days**: Number of days the pass is active
   - **Device/Door**: Select which device and door for access
3. Click **Create**
4. A QR code is generated and displayed

#### QR Code Display
- The QR code can be printed or saved
- Share with the visitor for scanning at the entrance
- The pass code is unique and tamper-proof

#### Scanning QR Passes
- At the entrance, the **QR Scan Window** accepts scanner input
- The system validates: pass code, expiry date, remaining uses
- Access is granted or denied with visual feedback

### 4.9 Monitor Display

Display real-time access events on a secondary monitor (e.g., entrance TV).

#### Setup
1. Connect a secondary monitor/TV to your PC
2. Click **Monitor** in the sidebar
3. The monitor window automatically detects and positions on the secondary display
4. Shows: player photo, name, access status, timestamp

#### Controls
- **Start/Stop**: Toggle real-time monitoring
- **Projector Mode**: Optimize display for projection
- **Full Screen**: Toggle full-screen mode

### 4.10 Logs & Audit

View session activity logs.

- All operations are logged: CREATE, UPDATE, DELETE, FREEZE, UNFREEZE, RENEW, ASSIGN_CARD, REMOVE_CARD, SYNC_CARD
- Each log entry includes: timestamp, operation, entity, user
- Logs are stored in `Logs/session.log`
- Session logs are reset on each app restart

### 4.11 Deleted Records

View and manage soft-deleted players.

#### Viewing Deleted Records
- Shows all players that were soft-deleted (not permanently removed)
- Displays: player name, deletion date, deleted by, reason

#### Restoring a Record
1. Select a deleted player
2. Click **Restore**
3. The player is restored to active status

### 4.12 Data Migration

Import and export data between AccessControlPro instances.

1. Click **Data Migration** in the sidebar
2. Choose **Import** or **Export**
3. Select tables to migrate
4. Follow the wizard to complete the transfer

---

## 5. Admin Panel

The Admin Panel provides system-level management.

### 5.1 User Management

Manage system users and their permissions.

#### Creating a User
1. Go to **Users**
2. Click **Add User**
3. Enter: Username, Password, Email
4. Assign **permissions** per application:

**Main App Permissions:**
| Permission | Description |
|------------|-------------|
| Dashboard | View dashboard |
| Devices | View devices |
| DevicesAdd/Edit/Delete/Connect | Device management |
| Doors | View doors |
| DoorsAdd/Edit/Delete | Door management |
| Players | View players |
| PlayersAdd/Edit/Delete | Player CRUD |
| PlayersAssignCard/RemoveCard | Card operations |
| PlayersFreeze/Renew | Subscription operations |
| PlayersReports | Player reports |
| Events | View events |
| Finance/CashFlow | Financial access |
| FinanceManage/CashFlowManage | Financial modifications |
| Logs/DeletedRecords | View logs/deleted |
| Monitor | Monitor display |
| QrPass | QR pass management |

5. Click **Save**

#### Editing Permissions
1. Select a user
2. Click **Edit**
3. Modify permissions as needed
4. Click **Save**

### 5.2 Categories & Products

#### Managing Categories
- Add product categories (Drinks, Supplements, Gear, Accessories, etc.)
- Categories are used in the POS terminal for product organization
- Each category has English and Arabic names

#### Managing Products
1. Go to **Products**
2. Click **Add Product**
3. Enter:
   - **Name (EN/AR)**: Product name in both languages
   - **Barcode**: Product barcode (optional, must be unique)
   - **Price**: Selling price
   - **Category**: Product category
   - **Stock**: Initial stock quantity
4. Click **Save**

### 5.3 Suppliers & Purchase Orders

#### Managing Suppliers
1. Go to **Suppliers**
2. Click **Add Supplier**
3. Enter: Name, Phone, Address, Contact Person
4. Click **Save**

#### Creating Purchase Orders
1. Go to **Purchase Orders**
2. Click **New Order**
3. Select **Supplier**
4. Add products with quantities and unit costs
5. Set **discount** (if any) and **amount paid**
6. Click **Create**
7. Stock is automatically updated for each product
8. An expense transaction is automatically created

#### Payment Tracking
- Purchase orders track: Total, Discount, Amount Paid, Status (Paid/Partial/Unpaid)
- Make additional payments by clicking **Pay** on a purchase order

### 5.4 Reports

Generate business reports with date filtering:
- **Membership Reports**: Active, expired, frozen players
- **Financial Reports**: Revenue, expenses, profit by period
- **Attendance Reports**: Access events by player/door
- **Device Reports**: Device uptime and event counts

### 5.5 Audit Log

View all security-related events:
- User logins/logouts
- Data modifications (create, update, delete)
- Permission changes
- Card operations
- Device connections
- Filter by date, user, action type

### 5.6 Backup & Restore

#### Creating a Backup
1. Go to **Backup**
2. Click **Create Backup**
3. The database is backed up to the configured location
4. Backup file includes timestamp in the filename

#### Restoring from Backup
1. Select a backup from the list
2. Click **Restore**
3. Confirm the restoration (this will overwrite current data)

### 5.7 Settings

Configure system-wide settings:
- **Gym Name / Company Name**: Displayed in reports
- **Currency Symbol**: Used in all financial displays
- **Logo**: Gym and developer logos
- **Address / Phone**: Contact information
- **Password Policy**: Minimum length, complexity requirements

---

## 6. POS Terminal

The Point-of-Sale terminal for product sales.

### 6.1 Selling Products

#### Quick Sale
1. Browse products by **category tabs** on the left side
2. Click a product to add it to the cart
3. Adjust **quantity** in the cart if needed
4. Click **Checkout**
5. Select **payment method**:
   - **Cash**: Direct cash payment
   - **Card Balance**: Deduct from player's card balance
6. Confirm the sale
7. Stock is automatically deducted
8. Transaction is logged

#### Cart Management
- **Remove item**: Click the X button next to an item
- **Change quantity**: Edit the quantity field directly
- **Clear cart**: Remove all items at once
- **Total**: Automatically calculated as items are added/removed

### 6.2 Card Balance & Top-Up

#### Checking Balance
1. Search for a player by name in the search bar
2. The player's card balance is displayed
3. Balance is shown in the selected currency

#### Topping Up Balance
1. Search and select a player
2. Click **Top Up**
3. Enter the top-up amount
4. Confirm the payment
5. Balance is updated immediately
6. An income transaction is recorded

### 6.3 Barcode Scanner

- Connect a USB barcode scanner to the POS PC
- Scan any product barcode
- The product is automatically found and added to the cart
- If the barcode is not found, an error message is displayed

#### Today's Sales
- The top bar shows today's total sales count and amount
- Click **Refresh** to update the figures

---

## 7. License Activation

### First-Time Activation
1. On first launch, the **Activation Window** appears
2. Your **Machine ID** is displayed (unique to your PC)
3. Contact the software provider with your Machine ID
4. Enter the **Activation Code** provided
5. Click **Activate**

### License Validity
- License is tied to your specific machine (hardware-based)
- A warning appears 15 days before license expiry
- Contact your provider to renew the license before it expires

---

## 8. Security Features

AccessControlPro includes enterprise-grade security:

| Feature | Description |
|---------|-------------|
| **Password Hashing** | PBKDF2 with salting for all user passwords |
| **Data Encryption** | AES-256 encryption for sensitive data |
| **DPAPI** | Windows-native encryption for device passwords |
| **Brute Force Protection** | Account lockout after failed attempts (DB-persistent) |
| **Rate Limiting** | 100 card operations per minute per device |
| **SQL Injection Prevention** | Parameterized queries + table name whitelist |
| **Audit Trail** | All operations logged with user, timestamp, details |
| **Concurrency Control** | Row versioning prevents lost updates |
| **Permission System** | Granular per-feature permissions per user |
| **Photo Encryption** | GDPR-compliant photo storage |

---

## 9. Troubleshooting

### Cannot Connect to Database
- Verify SQL Server is running
- Check `appsettings.json` connection string
- Ensure the SQL Server allows TCP/IP connections
- Check firewall settings for port 1433

### Device Not Connecting
- Verify the device is powered on and on the same network
- Check the IP address and port in device settings
- Ensure no firewall is blocking the device port
- Try the **Search Network** feature to rediscover devices

### Card Not Syncing to Device
- Check that the device is **online** (green indicator)
- Verify the card number is correct
- Ensure the device is selected as a sync target
- Check the rate limiter (max 100 operations per minute per device)

### Forgot Admin Password
- Contact your system administrator or software provider
- A password reset requires direct database access

### Application Crashes
- Check the `crash_log.txt` file in the application directory
- Check `Logs/session.log` for operation-level details
- Ensure SQL Server is accessible
- Verify sufficient disk space and memory

### License Issues
- Ensure the machine hardware has not changed significantly
- Contact the provider with your current Machine ID
- License is hardware-bound and cannot be transferred without a new key

---

**AccessControlPro v3.4** | Built with .NET 8 | Self-Contained Deployment
