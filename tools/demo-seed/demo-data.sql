-- =====================================================================
--  DEMO DATA — for screenshots and demos only
--  =====================================================================
--  ⚠️  ONLY RUN ON A FRESH DEMO DATABASE!
--      Do NOT run on production / customer data.
--
--  Setup:
--      1. Create a fresh database named "AccessControlPro_Demo" in SSMS
--      2. Run AccessControlPro.WPF.exe once → setup wizard appears
--      3. In wizard: Server=localhost, Database=AccessControlPro_Demo,
--                    User=sa, Password=YOUR_PASSWORD → Test → Save
--      4. Close the app
--      5. Run THIS script in SSMS against AccessControlPro_Demo
--      6. Reopen the app → take screenshots
--
--  Includes:
--      - 12 realistic players (English + Arabic names)
--      - 2 access devices, 3 doors
--      - 12 access cards (one per player)
--      - 30 recent access events (last 24 hours of activity)
--      - 10 transactions for the Finance screen
--      - Varied states: active, expiring soon, expired, frozen
-- =====================================================================

USE AccessControlPro_Demo;
GO

-- ---------------------------------------------------------------------
-- 1. Devices and Doors
-- ---------------------------------------------------------------------
SET IDENTITY_INSERT Devices ON;
INSERT INTO Devices (Id, Name, SerialNumber, IP, MAC, TCPPort, Username, Password, IsActive, CreatedAt)
VALUES
    (1, N'Main Entrance Controller', N'CA-3240T44080001', N'192.168.1.10', N'00:1A:2B:3C:4D:5E', 8000, N'admin', N'12345', 1, GETUTCDATE()),
    (2, N'Side Door Controller',     N'CA-3240T44080002', N'192.168.1.11', N'00:1A:2B:3C:4D:5F', 8000, N'admin', N'12345', 1, GETUTCDATE());
SET IDENTITY_INSERT Devices OFF;

SET IDENTITY_INSERT Doors ON;
INSERT INTO Doors (Id, DeviceId, Name, DoorNumber, IsActive, CreatedAt)
VALUES
    (1, 1, N'Front Door',  1, 1, GETUTCDATE()),
    (2, 1, N'Back Door',   2, 1, GETUTCDATE()),
    (3, 2, N'Side Door',   1, 1, GETUTCDATE());
SET IDENTITY_INSERT Doors OFF;
GO

-- ---------------------------------------------------------------------
-- 2. Players (Employees table) — 12 with varied states
-- ---------------------------------------------------------------------
SET IDENTITY_INSERT Employees ON;

-- Active monthly members
INSERT INTO Employees (Id, FullNameEn, FullNameAr, CardNo, Phone, SubscriptionType,
                       StartDate, EndDate, SubscriptionFee, AmountPaid, MaxVisits, UsedVisits,
                       IsFrozen, FreezeStartDate, IsDeleted, CreatedAt, PhotoData, Height, Weight, Notes, CardBalance)
VALUES
    -- 1. Recently joined, active
    (101, N'Ahmad Al-Hassan', N'أحمد الحسن', N'2001001', N'07712345001', N'Fitness',
     DATEADD(DAY, -10, GETUTCDATE()), DATEADD(DAY, 20, GETUTCDATE()),
     30000, 30000, 0, 0, 0, NULL, 0, DATEADD(DAY, -10, GETUTCDATE()), NULL, 0, 0, N'', 0),

    -- 2. Long-term member, half-paid
    (102, N'Sarah Mohammed', N'سارة محمد', N'2001002', N'07812345002', N'Fitness',
     DATEADD(DAY, -45, GETUTCDATE()), DATEADD(DAY, 45, GETUTCDATE()),
     90000, 50000, 0, 0, 0, NULL, 0, DATEADD(DAY, -45, GETUTCDATE()), NULL, 0, 0, N'', 0),

    -- 3. Expiring in 5 days
    (103, N'Omar Khalid', N'عمر خالد', N'2001003', N'07912345003', N'Fitness',
     DATEADD(DAY, -25, GETUTCDATE()), DATEADD(DAY, 5, GETUTCDATE()),
     30000, 30000, 0, 0, 0, NULL, 0, DATEADD(DAY, -25, GETUTCDATE()), NULL, 175, 70, N'', 0),

    -- 4. Frozen (on holiday)
    (104, N'Fatima Ali', N'فاطمة علي', N'2001004', N'07712345004', N'Fitness',
     DATEADD(DAY, -20, GETUTCDATE()), DATEADD(DAY, 10, GETUTCDATE()),
     30000, 30000, 0, 0, 1, DATEADD(DAY, -3, GETUTCDATE()), 0, DATEADD(DAY, -20, GETUTCDATE()), NULL, 0, 0, N'On vacation 2 weeks', 0),

    -- 5. Expired 3 days ago
    (105, N'Yusuf Ibrahim', N'يوسف إبراهيم', N'2001005', N'07812345005', N'Fitness',
     DATEADD(DAY, -33, GETUTCDATE()), DATEADD(DAY, -3, GETUTCDATE()),
     30000, 30000, 0, 0, 0, NULL, 0, DATEADD(DAY, -33, GETUTCDATE()), NULL, 0, 0, N'', 0),

    -- 6. Quarterly member, fresh
    (106, N'Maryam Hussein', N'مريم حسين', N'2001006', N'07912345006', N'Fitness',
     DATEADD(DAY, -5, GETUTCDATE()), DATEADD(DAY, 85, GETUTCDATE()),
     85000, 85000, 0, 0, 0, NULL, 0, DATEADD(DAY, -5, GETUTCDATE()), NULL, 0, 0, N'', 0),

    -- 7. Active monthly
    (107, N'Hassan Mahmoud', N'حسن محمود', N'2001007', N'07712345007', N'Fitness',
     DATEADD(DAY, -15, GETUTCDATE()), DATEADD(DAY, 15, GETUTCDATE()),
     30000, 30000, 0, 0, 0, NULL, 0, DATEADD(DAY, -15, GETUTCDATE()), NULL, 0, 0, N'', 0),

    -- 8. Brand new, joined today
    (108, N'Nour El-Din', N'نور الدين', N'2001008', N'07812345008', N'Fitness',
     DATEADD(HOUR, -2, GETUTCDATE()), DATEADD(DAY, 30, GETUTCDATE()),
     30000, 25000, 0, 0, 0, NULL, 0, DATEADD(HOUR, -2, GETUTCDATE()), NULL, 0, 0, N'', 0),

    -- 9. Expiring tomorrow (urgent renewal)
    (109, N'Reem Awad', N'ريم عوض', N'2001009', N'07912345009', N'Fitness',
     DATEADD(DAY, -29, GETUTCDATE()), DATEADD(DAY, 1, GETUTCDATE()),
     30000, 30000, 0, 0, 0, NULL, 0, DATEADD(DAY, -29, GETUTCDATE()), NULL, 0, 0, N'', 0),

    -- 10. Quarterly member
    (110, N'Khalid Saleh', N'خالد صالح', N'2001010', N'07712345010', N'Fitness',
     DATEADD(DAY, -30, GETUTCDATE()), DATEADD(DAY, 60, GETUTCDATE()),
     85000, 85000, 0, 0, 0, NULL, 0, DATEADD(DAY, -30, GETUTCDATE()), NULL, 0, 0, N'', 0),

    -- 11. Active monthly
    (111, N'Aisha Rahman', N'عائشة رحمان', N'2001011', N'07812345011', N'Fitness',
     DATEADD(DAY, -8, GETUTCDATE()), DATEADD(DAY, 22, GETUTCDATE()),
     30000, 30000, 0, 0, 0, NULL, 0, DATEADD(DAY, -8, GETUTCDATE()), NULL, 0, 0, N'', 0),

    -- 12. Frozen player
    (112, N'Tariq Nassar', N'طارق نصار', N'2001012', N'07912345012', N'Fitness',
     DATEADD(DAY, -18, GETUTCDATE()), DATEADD(DAY, 12, GETUTCDATE()),
     30000, 30000, 0, 0, 1, DATEADD(DAY, -7, GETUTCDATE()), 0, DATEADD(DAY, -18, GETUTCDATE()), NULL, 0, 0, N'Medical leave', 0);

SET IDENTITY_INSERT Employees OFF;
GO

-- ---------------------------------------------------------------------
-- 3. AccessCards — one per player
-- ---------------------------------------------------------------------
SET IDENTITY_INSERT AccessCards ON;
INSERT INTO AccessCards (Id, EmployeeId, CardNumber, IsActive, ValidFrom, ValidTo, EffectiveTimes, CreatedAt)
VALUES
    (1,  101, N'2001001', 1, DATEADD(DAY, -10, GETUTCDATE()), DATEADD(DAY, 20, GETUTCDATE()), 65535, DATEADD(DAY, -10, GETUTCDATE())),
    (2,  102, N'2001002', 1, DATEADD(DAY, -45, GETUTCDATE()), DATEADD(DAY, 45, GETUTCDATE()), 65535, DATEADD(DAY, -45, GETUTCDATE())),
    (3,  103, N'2001003', 1, DATEADD(DAY, -25, GETUTCDATE()), DATEADD(DAY, 5,  GETUTCDATE()), 65535, DATEADD(DAY, -25, GETUTCDATE())),
    (4,  104, N'2001004', 0, DATEADD(DAY, -20, GETUTCDATE()), DATEADD(DAY, 10, GETUTCDATE()), 65535, DATEADD(DAY, -20, GETUTCDATE())),
    (5,  105, N'2001005', 0, DATEADD(DAY, -33, GETUTCDATE()), DATEADD(DAY, -3, GETUTCDATE()), 65535, DATEADD(DAY, -33, GETUTCDATE())),
    (6,  106, N'2001006', 1, DATEADD(DAY, -5,  GETUTCDATE()), DATEADD(DAY, 85, GETUTCDATE()), 65535, DATEADD(DAY, -5,  GETUTCDATE())),
    (7,  107, N'2001007', 1, DATEADD(DAY, -15, GETUTCDATE()), DATEADD(DAY, 15, GETUTCDATE()), 65535, DATEADD(DAY, -15, GETUTCDATE())),
    (8,  108, N'2001008', 1, DATEADD(HOUR, -2, GETUTCDATE()), DATEADD(DAY, 30, GETUTCDATE()), 65535, DATEADD(HOUR, -2, GETUTCDATE())),
    (9,  109, N'2001009', 1, DATEADD(DAY, -29, GETUTCDATE()), DATEADD(DAY, 1,  GETUTCDATE()), 65535, DATEADD(DAY, -29, GETUTCDATE())),
    (10, 110, N'2001010', 1, DATEADD(DAY, -30, GETUTCDATE()), DATEADD(DAY, 60, GETUTCDATE()), 65535, DATEADD(DAY, -30, GETUTCDATE())),
    (11, 111, N'2001011', 1, DATEADD(DAY, -8,  GETUTCDATE()), DATEADD(DAY, 22, GETUTCDATE()), 65535, DATEADD(DAY, -8,  GETUTCDATE())),
    (12, 112, N'2001012', 0, DATEADD(DAY, -18, GETUTCDATE()), DATEADD(DAY, 12, GETUTCDATE()), 65535, DATEADD(DAY, -18, GETUTCDATE()));
SET IDENTITY_INSERT AccessCards OFF;
GO

-- ---------------------------------------------------------------------
-- 4. AccessEvents — recent activity for the dashboard / events screen
-- ---------------------------------------------------------------------
-- Mix of swipes throughout last 24 hours. EventType=1 (Card), EventCode=1 (Granted)
INSERT INTO AccessEvents (DoorId, CardId, EventType, EventCode, [Timestamp], Details)
VALUES
    -- This morning's activity
    (1, 1,  1, 1, DATEADD(MINUTE,  -5, GETUTCDATE()), N'Card swipe granted'),
    (1, 6,  1, 1, DATEADD(MINUTE, -12, GETUTCDATE()), N'Card swipe granted'),
    (1, 7,  1, 1, DATEADD(MINUTE, -25, GETUTCDATE()), N'Card swipe granted'),
    (1, 11, 1, 1, DATEADD(MINUTE, -38, GETUTCDATE()), N'Card swipe granted'),
    (1, 2,  1, 1, DATEADD(MINUTE, -45, GETUTCDATE()), N'Card swipe granted'),
    (1, 10, 1, 1, DATEADD(MINUTE, -57, GETUTCDATE()), N'Card swipe granted'),
    (1, 8,  1, 1, DATEADD(MINUTE, -68, GETUTCDATE()), N'Card swipe granted'),
    (2, 3,  1, 1, DATEADD(MINUTE, -85, GETUTCDATE()), N'Card swipe granted'),
    (1, 9,  1, 1, DATEADD(MINUTE,-105, GETUTCDATE()), N'Card swipe granted'),
    (1, 1,  1, 1, DATEADD(MINUTE,-145, GETUTCDATE()), N'Card swipe granted'),
    -- Few hours ago
    (1, 6,  1, 1, DATEADD(HOUR, -3, GETUTCDATE()), N'Card swipe granted'),
    (1, 7,  1, 1, DATEADD(HOUR, -3, GETUTCDATE()), N'Card swipe granted'),
    (1, 5,  1, 0, DATEADD(HOUR, -3, GETUTCDATE()), N'Access denied: card expired'),
    (1, 11, 1, 1, DATEADD(HOUR, -4, GETUTCDATE()), N'Card swipe granted'),
    (1, 10, 1, 1, DATEADD(HOUR, -4, GETUTCDATE()), N'Card swipe granted'),
    (1, 2,  1, 1, DATEADD(HOUR, -5, GETUTCDATE()), N'Card swipe granted'),
    (3, 8,  1, 1, DATEADD(HOUR, -5, GETUTCDATE()), N'Card swipe granted'),
    (1, 9,  1, 1, DATEADD(HOUR, -6, GETUTCDATE()), N'Card swipe granted'),
    -- Yesterday evening
    (1, 1,  1, 1, DATEADD(HOUR,-12, GETUTCDATE()), N'Card swipe granted'),
    (1, 7,  1, 1, DATEADD(HOUR,-13, GETUTCDATE()), N'Card swipe granted'),
    (1, 11, 1, 1, DATEADD(HOUR,-13, GETUTCDATE()), N'Card swipe granted'),
    (2, 4,  1, 0, DATEADD(HOUR,-14, GETUTCDATE()), N'Access denied: card frozen'),
    (1, 6,  1, 1, DATEADD(HOUR,-14, GETUTCDATE()), N'Card swipe granted'),
    (1, 10, 1, 1, DATEADD(HOUR,-15, GETUTCDATE()), N'Card swipe granted'),
    (1, 2,  1, 1, DATEADD(HOUR,-16, GETUTCDATE()), N'Card swipe granted'),
    (1, 8,  1, 1, DATEADD(HOUR,-17, GETUTCDATE()), N'Card swipe granted'),
    (1, 9,  1, 1, DATEADD(HOUR,-18, GETUTCDATE()), N'Card swipe granted'),
    (1, 3,  1, 1, DATEADD(HOUR,-19, GETUTCDATE()), N'Card swipe granted'),
    (1, 1,  1, 1, DATEADD(HOUR,-20, GETUTCDATE()), N'Card swipe granted'),
    (1, 7,  1, 1, DATEADD(HOUR,-22, GETUTCDATE()), N'Card swipe granted');
GO

-- ---------------------------------------------------------------------
-- 5. Transactions — for the Finance screen
-- ---------------------------------------------------------------------
-- Type 0 = Subscription Income, Type 1 = POS Sale Income, Type 2 = Expense
INSERT INTO Transactions (Type, Category, Amount, Description, RelatedEmployeeId, PaymentMethod, CreatedBy, CreatedAt, DiscountAmount, DiscountReason)
VALUES
    -- Today's subscription payments
    (0, N'Subscription', 30000, N'Monthly fitness — Ahmad Al-Hassan',     101, 0, N'admin', DATEADD(HOUR, -2,  GETUTCDATE()), 0, N''),
    (0, N'Subscription', 25000, N'Monthly fitness (partial) — Nour El-Din', 108, 0, N'admin', DATEADD(HOUR, -2,  GETUTCDATE()), 0, N''),
    (0, N'Subscription', 85000, N'Quarterly — Maryam Hussein',            106, 0, N'admin', DATEADD(HOUR, -5,  GETUTCDATE()), 0, N''),

    -- POS sales today
    (1, N'POS Sale', 3000,  N'Water bottle (1L)',   NULL, 0, N'admin', DATEADD(HOUR, -1, GETUTCDATE()), 0, N''),
    (1, N'POS Sale', 12000, N'Protein shake',       NULL, 0, N'admin', DATEADD(HOUR, -3, GETUTCDATE()), 0, N''),
    (1, N'POS Sale', 5000,  N'Energy drink',        NULL, 1, N'admin', DATEADD(HOUR, -4, GETUTCDATE()), 0, N''),
    (1, N'POS Sale', 8000,  N'Towel',               NULL, 0, N'admin', DATEADD(HOUR, -6, GETUTCDATE()), 0, N''),

    -- Earlier this week
    (0, N'Subscription', 30000, N'Monthly — Hassan Mahmoud',   107, 0, N'admin', DATEADD(DAY, -2, GETUTCDATE()), 0, N''),
    (0, N'Subscription', 30000, N'Monthly — Aisha Rahman',     111, 0, N'admin', DATEADD(DAY, -3, GETUTCDATE()), 0, N''),
    (0, N'Subscription', 85000, N'Quarterly — Khalid Saleh',   110, 0, N'admin', DATEADD(DAY, -5, GETUTCDATE()), 0, N''),

    -- An expense
    (2, N'Expense', 50000, N'Cleaning supplies',    NULL, 0, N'admin', DATEADD(DAY, -1, GETUTCDATE()), 0, N'');
GO

-- ---------------------------------------------------------------------
-- 6. SubscriptionPlans — typical gym plans
-- ---------------------------------------------------------------------
SET IDENTITY_INSERT SubscriptionPlans ON;
INSERT INTO SubscriptionPlans (Id, NameEn, NameAr, Duration, DurationType, Price, MaxVisits, EffectiveTimes, IsActive, SortOrder, CreatedAt)
VALUES
    (1, N'Monthly Fitness',   N'اشتراك شهري',   30,  N'Days',   30000, 0, 65535, 1, 1, GETUTCDATE()),
    (2, N'Quarterly Fitness', N'اشتراك ربع سنوي', 90,  N'Days',   85000, 0, 65535, 1, 2, GETUTCDATE()),
    (3, N'Half-Year',         N'اشتراك نصف سنوي', 180, N'Days', 160000, 0, 65535, 1, 3, GETUTCDATE()),
    (4, N'Annual',            N'اشتراك سنوي',     365, N'Days', 300000, 0, 65535, 1, 4, GETUTCDATE()),
    (5, N'Visit Pass (10)',   N'بطاقة زيارات (10)', 30, N'Days',  35000, 10, 65535, 1, 5, GETUTCDATE());
SET IDENTITY_INSERT SubscriptionPlans OFF;
GO

-- ---------------------------------------------------------------------
-- 7. Products — for POS screen
-- ---------------------------------------------------------------------
SET IDENTITY_INSERT Products ON;
INSERT INTO Products (Id, Name, NameAr, Price, Category, Stock, IsActive, CreatedAt)
VALUES
    (1, N'Water Bottle 1L',    N'قنينة ماء 1 لتر',     3000, N'Drinks',     50, 1, GETUTCDATE()),
    (2, N'Energy Drink',       N'مشروب طاقة',           5000, N'Drinks',     30, 1, GETUTCDATE()),
    (3, N'Protein Shake',      N'مخفوق البروتين',     12000, N'Supplements', 20, 1, GETUTCDATE()),
    (4, N'Gym Towel',          N'منشفة الصالة',        8000, N'Accessories', 15, 1, GETUTCDATE()),
    (5, N'Branded T-Shirt',    N'تي شيرت بشعار',      25000, N'Apparel',     10, 1, GETUTCDATE()),
    (6, N'Whey Protein 1kg',   N'بروتين واي 1 كغ',    85000, N'Supplements',  8, 1, GETUTCDATE()),
    (7, N'Gym Lock',           N'قفل خزانة',           5000, N'Accessories', 25, 1, GETUTCDATE());
SET IDENTITY_INSERT Products OFF;
GO

-- ---------------------------------------------------------------------
-- 8. AppSettings — gym branding
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM AppSettings)
BEGIN
    INSERT INTO AppSettings (GymName) VALUES (N'HM-GymManagement Demo');
END
ELSE
BEGIN
    UPDATE AppSettings SET GymName = N'HM-GymManagement Demo' WHERE Id = 1;
END
GO

PRINT '✅ Demo data seeded!';
PRINT '   - 12 players (mix of active/expiring/expired/frozen)';
PRINT '   - 12 access cards';
PRINT '   - 30 recent access events';
PRINT '   - 11 transactions (subscriptions + POS sales + expense)';
PRINT '   - 5 subscription plans';
PRINT '   - 7 products';
PRINT '';
PRINT '📸 Now reopen the app to take screenshots!';
GO
