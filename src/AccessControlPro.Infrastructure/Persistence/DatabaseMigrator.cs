using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence;

/// <summary>
/// Centralized database migration — ensures schema is up-to-date across all apps.
/// All 3 apps (Main, Admin, POS) call this on startup so whichever runs first applies changes.
/// Uses IF NOT EXISTS guards so migrations are idempotent and safe for parallel startup.
/// </summary>
public static class DatabaseMigrator
{
    public static void EnsureSchemaUpToDate(AppDbContext db)
    {
        // Step 1: Create database + tables from EF model if DB is brand new
        db.Database.EnsureCreated();

        // Step 2: Incremental migrations for columns/tables added after initial release.
        // Each statement is idempotent — safe to run multiple times or concurrently.
        var migrations = new[]
        {
            // v1.1: Add CardBalance to Employees
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'CardBalance')
              ALTER TABLE Employees ADD CardBalance decimal(18,2) NOT NULL DEFAULT 0;",

            // v4.6.7: Player POS credit debt ("on account" sales) — separate from CardBalance
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'Debt')
              ALTER TABLE Employees ADD Debt decimal(18,2) NOT NULL DEFAULT 0;",

            // v1.2: Add Permissions to Users
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'Permissions')
              ALTER TABLE Users ADD Permissions nvarchar(max) NOT NULL DEFAULT '';",

            // v1.3: Transactions table
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Transactions')
              CREATE TABLE Transactions (
                  Id int IDENTITY(1,1) PRIMARY KEY,
                  Type int NOT NULL,
                  Category nvarchar(200) NOT NULL DEFAULT '',
                  Amount decimal(18,2) NOT NULL,
                  Description nvarchar(1000) NOT NULL DEFAULT '',
                  RelatedEmployeeId int NULL,
                  PaymentMethod int NOT NULL DEFAULT 0,
                  CreatedBy nvarchar(100) NOT NULL DEFAULT '',
                  CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
                  CONSTRAINT FK_Transactions_Employees FOREIGN KEY (RelatedEmployeeId) REFERENCES Employees(Id) ON DELETE SET NULL
              );",

            // v1.4: Products table
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Products')
              CREATE TABLE Products (
                  Id int IDENTITY(1,1) PRIMARY KEY,
                  Name nvarchar(200) NOT NULL DEFAULT '',
                  NameAr nvarchar(200) NOT NULL DEFAULT '',
                  Price decimal(18,2) NOT NULL,
                  CostPrice decimal(18,2) NOT NULL DEFAULT 0,
                  Category nvarchar(100) NOT NULL DEFAULT '',
                  Stock int NOT NULL DEFAULT 0,
                  IsActive bit NOT NULL DEFAULT 1,
                  CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
              );",

            // v4.6.7: CostPrice on existing Products tables (profit/margin tracking)
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'CostPrice')
              ALTER TABLE Products ADD CostPrice decimal(18,2) NOT NULL DEFAULT 0;",

            // v4.6.9: Reference link on Transactions (e.g. "PO-12") so a purchase-order's
            // expense entry can be found and kept in sync when the PO is edited.
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Transactions') AND name = 'Reference')
              ALTER TABLE Transactions ADD Reference nvarchar(100) NOT NULL DEFAULT '';",

            // v4.6.13: Gym owner name on AppSettings — printed on receipts.
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AppSettings') AND name = 'Owner')
              ALTER TABLE AppSettings ADD Owner nvarchar(200) NOT NULL DEFAULT '';",

            // v1.5: Indexes for performance (50+ concurrent users)
            // Transactions: filter by type, date range, employee
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transactions_CreatedAt' AND object_id = OBJECT_ID('Transactions'))
              CREATE INDEX IX_Transactions_CreatedAt ON Transactions(CreatedAt);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transactions_Type' AND object_id = OBJECT_ID('Transactions'))
              CREATE INDEX IX_Transactions_Type ON Transactions(Type);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transactions_RelatedEmployeeId' AND object_id = OBJECT_ID('Transactions'))
              CREATE INDEX IX_Transactions_RelatedEmployeeId ON Transactions(RelatedEmployeeId);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transactions_Type_CreatedAt' AND object_id = OBJECT_ID('Transactions'))
              CREATE INDEX IX_Transactions_Type_CreatedAt ON Transactions(Type, CreatedAt) INCLUDE (Amount, Category);",

            // Products: filter by active, category
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_IsActive' AND object_id = OBJECT_ID('Products'))
              CREATE INDEX IX_Products_IsActive ON Products(IsActive);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_Category' AND object_id = OBJECT_ID('Products'))
              CREATE INDEX IX_Products_Category ON Products(Category);",

            // AccessEvents: filter by DoorId + Timestamp (common query)
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessEvents_DoorId_Timestamp' AND object_id = OBJECT_ID('AccessEvents'))
              CREATE INDEX IX_AccessEvents_DoorId_Timestamp ON AccessEvents(DoorId, Timestamp);",

            // AccessEvents: filter by CardId
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessEvents_CardId' AND object_id = OBJECT_ID('AccessEvents'))
              CREATE INDEX IX_AccessEvents_CardId ON AccessEvents(CardId);",

            // Employees: IsFrozen for quick frozen-player queries
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_IsFrozen' AND object_id = OBJECT_ID('Employees'))
              CREATE INDEX IX_Employees_IsFrozen ON Employees(IsFrozen) WHERE IsFrozen = 1;",

            // Employees: StartDate for renewal reports
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_StartDate' AND object_id = OBJECT_ID('Employees'))
              CREATE INDEX IX_Employees_StartDate ON Employees(StartDate);",

            // Users: IsActive for login queries
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_IsActive' AND object_id = OBJECT_ID('Users'))
              CREATE INDEX IX_Users_IsActive ON Users(IsActive);",

            // AuditLogs: Action filter
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_Action' AND object_id = OBJECT_ID('AuditLogs'))
              CREATE INDEX IX_AuditLogs_Action ON AuditLogs(Action);",

            // DeletedEmployees: DeletedBy for filtering
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeletedEmployees_DeletedBy' AND object_id = OBJECT_ID('DeletedEmployees'))
              CREATE INDEX IX_DeletedEmployees_DeletedBy ON DeletedEmployees(DeletedBy);",

            // AccessCards: EmployeeId for fast joins
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessCards_EmployeeId' AND object_id = OBJECT_ID('AccessCards'))
              CREATE INDEX IX_AccessCards_EmployeeId ON AccessCards(EmployeeId);",

            // v1.6: Optimistic concurrency token on Employees (prevents lost updates with 50+ users)
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'RowVersion')
              ALTER TABLE Employees ADD RowVersion rowversion NOT NULL;",

            // v1.8: LookupItems table (admin-configurable dropdown values)
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LookupItems')
            BEGIN
              CREATE TABLE LookupItems (
                  Id int IDENTITY(1,1) PRIMARY KEY,
                  Category nvarchar(100) NOT NULL DEFAULT '',
                  Name nvarchar(200) NOT NULL DEFAULT '',
                  NameAr nvarchar(200) NOT NULL DEFAULT '',
                  NumericValue decimal(18,2) NOT NULL DEFAULT 0,
                  SortOrder int NOT NULL DEFAULT 0,
                  IsActive bit NOT NULL DEFAULT 1
              );
              CREATE INDEX IX_LookupItems_Category ON LookupItems(Category, IsActive, SortOrder);

              -- Income Categories
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('IncomeCategory', 'Subscription', N'اشتراك', 1);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('IncomeCategory', 'POS Sales', N'مبيعات', 2);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('IncomeCategory', 'Owner Deposit', N'إيداع المالك', 3);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('IncomeCategory', 'Other', N'أخرى', 99);

              -- Expense Categories
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ExpenseCategory', 'Rent', N'إيجار', 1);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ExpenseCategory', 'Electricity', N'كهرباء', 2);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ExpenseCategory', 'Water', N'ماء', 3);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ExpenseCategory', 'Salaries', N'رواتب', 4);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ExpenseCategory', 'Equipment', N'معدات', 5);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ExpenseCategory', 'Maintenance', N'صيانة', 6);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ExpenseCategory', 'Supplies', N'مستلزمات', 7);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ExpenseCategory', 'Marketing', N'تسويق', 8);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ExpenseCategory', 'Other', N'أخرى', 99);

              -- Product Categories
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ProductCategory', 'Drinks', N'مشروبات', 1);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ProductCategory', 'Supplements', N'مكملات', 2);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ProductCategory', 'Gear', N'ملابس رياضية', 3);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ProductCategory', 'Accessories', N'إكسسوارات', 4);
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('ProductCategory', 'Other', N'أخرى', 99);

              -- Subscription Plans (NumericValue = MonthlyRate)
              INSERT INTO LookupItems (Category, Name, NameAr, NumericValue, SortOrder) VALUES ('SubscriptionPlan', 'Fitness', N'لياقة بدنية', 25000, 1);
              INSERT INTO LookupItems (Category, Name, NameAr, NumericValue, SortOrder) VALUES ('SubscriptionPlan', 'Kickboxing', N'كيك بوكسينغ', 30000, 2);
              INSERT INTO LookupItems (Category, Name, NameAr, NumericValue, SortOrder) VALUES ('SubscriptionPlan', 'Swimming', N'سباحة', 20000, 3);
              INSERT INTO LookupItems (Category, Name, NameAr, NumericValue, SortOrder) VALUES ('SubscriptionPlan', 'CrossFit', N'كروس فيت', 35000, 4);
              INSERT INTO LookupItems (Category, Name, NameAr, NumericValue, SortOrder) VALUES ('SubscriptionPlan', 'Yoga', N'يوغا', 15000, 5);
              INSERT INTO LookupItems (Category, Name, NameAr, NumericValue, SortOrder) VALUES ('SubscriptionPlan', 'Full Access', N'وصول كامل', 50000, 6);
            END",

            // v1.7: AppSettings table (single-row company/gym settings)
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AppSettings')
            BEGIN
              CREATE TABLE AppSettings (
                  Id int IDENTITY(1,1) PRIMARY KEY,
                  CompanyName nvarchar(200) NOT NULL DEFAULT '',
                  GymName nvarchar(200) NOT NULL DEFAULT '',
                  LogoPath nvarchar(500) NOT NULL DEFAULT '',
                  Phone nvarchar(100) NOT NULL DEFAULT '',
                  Address nvarchar(500) NOT NULL DEFAULT ''
              );
              INSERT INTO AppSettings (CompanyName, GymName, LogoPath, Phone, Address) VALUES ('', '', '', '', '');
            END",

            // v1.9: Add Barcode column to Products
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'Barcode')
              ALTER TABLE Products ADD Barcode nvarchar(100) NOT NULL DEFAULT '';",

            // v1.9: Suppliers table
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Suppliers')
              CREATE TABLE Suppliers (
                  Id int IDENTITY(1,1) PRIMARY KEY,
                  Name nvarchar(200) NOT NULL DEFAULT '',
                  Phone nvarchar(100) NOT NULL DEFAULT '',
                  Address nvarchar(500) NOT NULL DEFAULT '',
                  ContactPerson nvarchar(200) NOT NULL DEFAULT '',
                  IsActive bit NOT NULL DEFAULT 1,
                  CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE()
              );",

            // v1.9: PurchaseOrders table
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PurchaseOrders')
              CREATE TABLE PurchaseOrders (
                  Id int IDENTITY(1,1) PRIMARY KEY,
                  SupplierId int NOT NULL,
                  OrderDate datetime2 NOT NULL DEFAULT GETUTCDATE(),
                  TotalAmount decimal(18,2) NOT NULL DEFAULT 0,
                  Notes nvarchar(1000) NOT NULL DEFAULT '',
                  CreatedBy nvarchar(100) NOT NULL DEFAULT '',
                  CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
                  CONSTRAINT FK_PurchaseOrders_Suppliers FOREIGN KEY (SupplierId) REFERENCES Suppliers(Id)
              );",

            // v1.9: PurchaseOrderItems table
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PurchaseOrderItems')
              CREATE TABLE PurchaseOrderItems (
                  Id int IDENTITY(1,1) PRIMARY KEY,
                  PurchaseOrderId int NOT NULL,
                  ProductId int NOT NULL,
                  Quantity int NOT NULL DEFAULT 0,
                  UnitCost decimal(18,2) NOT NULL DEFAULT 0,
                  CONSTRAINT FK_POItems_PurchaseOrders FOREIGN KEY (PurchaseOrderId) REFERENCES PurchaseOrders(Id) ON DELETE CASCADE,
                  CONSTRAINT FK_POItems_Products FOREIGN KEY (ProductId) REFERENCES Products(Id)
              );",

            // v1.9: Indexes for new tables
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseOrders_SupplierId' AND object_id = OBJECT_ID('PurchaseOrders'))
              CREATE INDEX IX_PurchaseOrders_SupplierId ON PurchaseOrders(SupplierId);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseOrderItems_PurchaseOrderId' AND object_id = OBJECT_ID('PurchaseOrderItems'))
              CREATE INDEX IX_PurchaseOrderItems_PurchaseOrderId ON PurchaseOrderItems(PurchaseOrderId);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_Barcode' AND object_id = OBJECT_ID('Products'))
              CREATE INDEX IX_Products_Barcode ON Products(Barcode) WHERE Barcode <> '';",

            // v2.0: StockMovements table — tracks every stock in/out
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StockMovements')
              CREATE TABLE StockMovements (
                  Id int IDENTITY(1,1) PRIMARY KEY,
                  ProductId int NOT NULL,
                  Type int NOT NULL,
                  Quantity int NOT NULL DEFAULT 0,
                  UnitPrice decimal(18,2) NOT NULL DEFAULT 0,
                  Reference nvarchar(200) NOT NULL DEFAULT '',
                  Description nvarchar(500) NOT NULL DEFAULT '',
                  PurchaseOrderId int NULL,
                  TransactionId int NULL,
                  CreatedBy nvarchar(100) NOT NULL DEFAULT '',
                  CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
                  CONSTRAINT FK_StockMovements_Products FOREIGN KEY (ProductId) REFERENCES Products(Id),
                  CONSTRAINT FK_StockMovements_PurchaseOrders FOREIGN KEY (PurchaseOrderId) REFERENCES PurchaseOrders(Id)
              );",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockMovements_ProductId' AND object_id = OBJECT_ID('StockMovements'))
              CREATE INDEX IX_StockMovements_ProductId ON StockMovements(ProductId);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockMovements_CreatedAt' AND object_id = OBJECT_ID('StockMovements'))
              CREATE INDEX IX_StockMovements_CreatedAt ON StockMovements(CreatedAt);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockMovements_Type' AND object_id = OBJECT_ID('StockMovements'))
              CREATE INDEX IX_StockMovements_Type ON StockMovements(Type);",

            // v2.1: Payment tracking on PurchaseOrders
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'Discount')
              ALTER TABLE PurchaseOrders ADD Discount decimal(18,2) NOT NULL DEFAULT 0;",

            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'AmountPaid')
              ALTER TABLE PurchaseOrders ADD AmountPaid decimal(18,2) NOT NULL DEFAULT 0;",

            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PurchaseOrders') AND name = 'PaymentStatus')
              ALTER TABLE PurchaseOrders ADD PaymentStatus nvarchar(50) NOT NULL DEFAULT 'Unpaid';",

            // v2.2: Additional indexes for search/filter performance
            // AccessEvents: EventType for filtered queries
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessEvents_EventType' AND object_id = OBJECT_ID('AccessEvents'))
              CREATE INDEX IX_AccessEvents_EventType ON AccessEvents(EventType);",

            // Transactions: Category for filtered queries
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transactions_Category' AND object_id = OBJECT_ID('Transactions'))
              CREATE INDEX IX_Transactions_Category ON Transactions(Category);",

            // Suppliers: IsActive for filtered queries
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Suppliers_IsActive' AND object_id = OBJECT_ID('Suppliers'))
              CREATE INDEX IX_Suppliers_IsActive ON Suppliers(IsActive) WHERE IsActive = 1;",

            // PurchaseOrders: OrderDate for date range queries
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseOrders_OrderDate' AND object_id = OBJECT_ID('PurchaseOrders'))
              CREATE INDEX IX_PurchaseOrders_OrderDate ON PurchaseOrders(OrderDate);",

            // FreezeHistory: Composite for active freeze lookup
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FreezeHistory_EmployeeId_FreezeEnd' AND object_id = OBJECT_ID('FreezeHistories'))
              CREATE INDEX IX_FreezeHistory_EmployeeId_FreezeEnd ON FreezeHistories(EmployeeId, FreezeEnd);",

            // DeletedEmployees: FullNameEn for search queries
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DeletedEmployees_FullNameEn' AND object_id = OBJECT_ID('DeletedEmployees'))
              CREATE INDEX IX_DeletedEmployees_FullNameEn ON DeletedEmployees(FullNameEn);",

            // v2.3: FK for StockMovements.TransactionId
            @"IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_StockMovements_Transactions')
              ALTER TABLE StockMovements ADD CONSTRAINT FK_StockMovements_Transactions
              FOREIGN KEY (TransactionId) REFERENCES Transactions(Id) ON DELETE SET NULL;",

            // v2.3: Unique index on Products.Barcode (only non-empty values)
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Products_Barcode' AND object_id = OBJECT_ID('Products'))
            BEGIN
              IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_Barcode' AND object_id = OBJECT_ID('Products'))
                DROP INDEX IX_Products_Barcode ON Products;
              CREATE UNIQUE INDEX UX_Products_Barcode ON Products(Barcode) WHERE Barcode <> '';
            END",

            // v2.3: Suppliers.Name index for search
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Suppliers_Name' AND object_id = OBJECT_ID('Suppliers'))
              CREATE INDEX IX_Suppliers_Name ON Suppliers(Name);",

            // v2.3: PurchaseOrderItems.ProductId index
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseOrderItems_ProductId' AND object_id = OBJECT_ID('PurchaseOrderItems'))
              CREATE INDEX IX_PurchaseOrderItems_ProductId ON PurchaseOrderItems(ProductId);",

            // v2.4: CHECK constraints to prevent negative balances/stock (DB-level safety net)
            @"IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Employees_CardBalance_NonNegative')
              ALTER TABLE Employees ADD CONSTRAINT CK_Employees_CardBalance_NonNegative CHECK (CardBalance >= 0);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Products_Stock_NonNegative')
              ALTER TABLE Products ADD CONSTRAINT CK_Products_Stock_NonNegative CHECK (Stock >= 0);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Products_Price_NonNegative')
              ALTER TABLE Products ADD CONSTRAINT CK_Products_Price_NonNegative CHECK (Price >= 0);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Transactions_Amount_Positive')
              ALTER TABLE Transactions ADD CONSTRAINT CK_Transactions_Amount_Positive CHECK (Amount >= 0);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PurchaseOrders_TotalAmount_NonNegative')
              ALTER TABLE PurchaseOrders ADD CONSTRAINT CK_PurchaseOrders_TotalAmount_NonNegative CHECK (TotalAmount >= 0);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PurchaseOrders_Discount_NonNegative')
              ALTER TABLE PurchaseOrders ADD CONSTRAINT CK_PurchaseOrders_Discount_NonNegative CHECK (Discount >= 0);",

            // v3.0: CardDeviceSyncs table — tracks which cards are synced to which devices
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CardDeviceSyncs')
              CREATE TABLE CardDeviceSyncs (
                  Id int IDENTITY(1,1) PRIMARY KEY,
                  AccessCardId int NOT NULL,
                  DeviceId int NOT NULL,
                  IsSynced bit NOT NULL DEFAULT 0,
                  SyncedAt datetime2 NULL,
                  LastError nvarchar(500) NULL,
                  CONSTRAINT FK_CardDeviceSyncs_AccessCards FOREIGN KEY (AccessCardId) REFERENCES AccessCards(Id) ON DELETE CASCADE,
                  CONSTRAINT FK_CardDeviceSyncs_Devices FOREIGN KEY (DeviceId) REFERENCES Devices(Id) ON DELETE CASCADE,
                  CONSTRAINT UQ_CardDeviceSyncs_Card_Device UNIQUE (AccessCardId, DeviceId)
              );",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CardDeviceSyncs_DeviceId' AND object_id = OBJECT_ID('CardDeviceSyncs'))
              CREATE INDEX IX_CardDeviceSyncs_DeviceId ON CardDeviceSyncs(DeviceId);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CardDeviceSyncs_AccessCardId' AND object_id = OBJECT_ID('CardDeviceSyncs'))
              CREATE INDEX IX_CardDeviceSyncs_AccessCardId ON CardDeviceSyncs(AccessCardId);",

            // v3.0: Visit-count subscription fields on Employees
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'MaxVisits')
              ALTER TABLE Employees ADD MaxVisits int NOT NULL DEFAULT 0;",

            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'UsedVisits')
              ALTER TABLE Employees ADD UsedVisits int NOT NULL DEFAULT 0;",

            // v3.1: QrPasses table — daily/short-term QR code passes for visitors
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QrPasses')
              CREATE TABLE QrPasses (
                  Id int IDENTITY(1,1) PRIMARY KEY,
                  PassCode nvarchar(100) NOT NULL,
                  PlayerName nvarchar(200) NOT NULL DEFAULT '',
                  Phone nvarchar(100) NOT NULL DEFAULT '',
                  ValidFrom datetime2 NOT NULL,
                  ValidTo datetime2 NOT NULL,
                  MaxUses int NOT NULL DEFAULT 5,
                  UsedCount int NOT NULL DEFAULT 0,
                  Fee decimal(18,2) NOT NULL DEFAULT 0,
                  IsActive bit NOT NULL DEFAULT 1,
                  CreatedBy nvarchar(100) NOT NULL DEFAULT '',
                  CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
                  CONSTRAINT UQ_QrPasses_PassCode UNIQUE (PassCode)
              );",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QrPasses_PassCode' AND object_id = OBJECT_ID('QrPasses'))
              CREATE INDEX IX_QrPasses_PassCode ON QrPasses(PassCode);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QrPasses_IsActive_ValidTo' AND object_id = OBJECT_ID('QrPasses'))
              CREATE INDEX IX_QrPasses_IsActive_ValidTo ON QrPasses(IsActive, ValidTo) WHERE IsActive = 1;",

            // v3.1: Add DailyPass income category to LookupItems
            @"IF NOT EXISTS (SELECT 1 FROM LookupItems WHERE Category = 'IncomeCategory' AND Name = 'Daily Pass')
              INSERT INTO LookupItems (Category, Name, NameAr, SortOrder) VALUES ('IncomeCategory', 'Daily Pass', N'تصريح يومي', 4);",

            // v3.2: Add Device/Door columns to QrPasses
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('QrPasses') AND name = 'DeviceId')
              ALTER TABLE QrPasses ADD DeviceId int NULL;",

            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('QrPasses') AND name = 'DoorNumber')
              ALTER TABLE QrPasses ADD DoorNumber int NOT NULL DEFAULT 1;",

            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('QrPasses') AND name = 'DeviceName')
              ALTER TABLE QrPasses ADD DeviceName nvarchar(200) NOT NULL DEFAULT '';",

            // v3.3: Add DevLogoPath to AppSettings (developer company logo)
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AppSettings') AND name = 'DevLogoPath')
              ALTER TABLE AppSettings ADD DevLogoPath nvarchar(500) NOT NULL DEFAULT '';",

            // v3.4: Performance indexes — hot query paths identified by profiling
            // AccessEvents: standalone Timestamp index for date-range-only queries (dashboard, reports)
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessEvents_Timestamp' AND object_id = OBJECT_ID('AccessEvents'))
              CREATE INDEX IX_AccessEvents_Timestamp ON AccessEvents(Timestamp DESC);",

            // Employees: CardNo lookup (used by card assignment, scan, sync)
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_CardNo' AND object_id = OBJECT_ID('Employees'))
              CREATE INDEX IX_Employees_CardNo ON Employees(CardNo) WHERE CardNo <> '';",

            // Employees: Outstanding balance query (SubscriptionFee > AmountPaid)
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_Outstanding' AND object_id = OBJECT_ID('Employees'))
              CREATE INDEX IX_Employees_Outstanding ON Employees(SubscriptionFee, AmountPaid) WHERE SubscriptionFee > AmountPaid;",

            // AccessCards: CardNumber for scan lookups
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccessCards_CardNumber' AND object_id = OBJECT_ID('AccessCards'))
              CREATE INDEX IX_AccessCards_CardNumber ON AccessCards(CardNumber);",

            // AuditLogs: Timestamp for recent audit queries
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_Timestamp' AND object_id = OBJECT_ID('AuditLogs'))
              CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp DESC);",

            // v3.5: MonitorLock table — ensures only ONE Real-Time Monitor runs across all PCs
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MonitorLocks')
              CREATE TABLE MonitorLocks (
                  Id INT IDENTITY(1,1) PRIMARY KEY,
                  MachineName NVARCHAR(200) NOT NULL,
                  UserName NVARCHAR(200) NOT NULL DEFAULT '',
                  AcquiredAt DATETIME2 NOT NULL,
                  HeartbeatAt DATETIME2 NOT NULL
              );",

            // v3.7: Door working schedule (hours + days)
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Doors') AND name = 'WorkStartTime')
              ALTER TABLE Doors ADD WorkStartTime TIME NOT NULL DEFAULT '00:00:00';",

            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Doors') AND name = 'WorkEndTime')
              ALTER TABLE Doors ADD WorkEndTime TIME NOT NULL DEFAULT '23:59:59';",

            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Doors') AND name = 'Is24Hours')
              ALTER TABLE Doors ADD Is24Hours BIT NOT NULL DEFAULT 1;",

            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Doors') AND name = 'WorkingDays')
              ALTER TABLE Doors ADD WorkingDays NVARCHAR(20) NOT NULL DEFAULT '1,2,3,4,5,6,7';",

            // v4.0: TimeGroups table — time-based access schedules for cards
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TimeGroups')
            BEGIN
                CREATE TABLE TimeGroups (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    NameEn NVARCHAR(100) NOT NULL,
                    NameAr NVARCHAR(100) NOT NULL DEFAULT '',
                    HardwareIndex INT NOT NULL DEFAULT 1,
                    IsDefault BIT NOT NULL DEFAULT 0,
                    ScheduleJson NVARCHAR(MAX) NOT NULL DEFAULT '{}',
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    UpdatedAt DATETIME2 NULL
                );

                CREATE UNIQUE INDEX UX_TimeGroups_HardwareIndex ON TimeGroups(HardwareIndex);

                -- Seed default 24/7 group
                INSERT INTO TimeGroups (NameEn, NameAr, HardwareIndex, IsDefault, ScheduleJson)
                VALUES ('24/7 Full Access', N'وصول كامل 24/7', 1, 1, '{}');
            END",

            // v4.1: QrPool table — pre-generated QR codes as virtual cards for guest access
            @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'QrPool')
            BEGIN
                CREATE TABLE QrPool (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Code VARCHAR(20) NOT NULL,
                    Status INT NOT NULL DEFAULT 0,
                    Source VARCHAR(10) NOT NULL DEFAULT 'Local',
                    GuestName NVARCHAR(200) DEFAULT '',
                    GuestPhone VARCHAR(50) DEFAULT '',
                    Reason NVARCHAR(500) DEFAULT '',
                    AssignedAt DATETIME2 NULL,
                    UsedAt DATETIME2 NULL,
                    ExpiredAt DATETIME2 NULL,
                    MaxUses INT DEFAULT 2,
                    UsedCount INT DEFAULT 0,
                    ValidFrom DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    ValidTo DATETIME2 NOT NULL,
                    DoorPermissions VARCHAR(20) DEFAULT '01010000',
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    IsUploadedToDevice BIT DEFAULT 0
                );
                CREATE UNIQUE INDEX IX_QrPool_Code ON QrPool(Code);
            END",

            // v3.6: Fix FK on AccessEvents.CardId → SET NULL on delete (allows employee/card deletion without constraint errors)
            @"DECLARE @fkName NVARCHAR(200);
              SELECT @fkName = fk.name
              FROM sys.foreign_keys fk
              JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
              JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
              WHERE fk.parent_object_id = OBJECT_ID('AccessEvents') AND c.name = 'CardId'
                AND fk.delete_referential_action <> 1; -- 1 = SET NULL, skip if already correct
              IF @fkName IS NOT NULL
              BEGIN
                  EXEC('ALTER TABLE AccessEvents DROP CONSTRAINT ' + @fkName);
                  ALTER TABLE AccessEvents ADD CONSTRAINT FK_AccessEvents_CardId
                      FOREIGN KEY (CardId) REFERENCES AccessCards(Id) ON DELETE SET NULL;
              END;",

            // v4.2: PosShifts table — cash drawer shift tracking
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PosShifts')
              CREATE TABLE PosShifts (
                  Id INT IDENTITY(1,1) PRIMARY KEY,
                  OpenedBy NVARCHAR(100) NOT NULL DEFAULT '',
                  OpenedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                  ClosedAt DATETIME2 NULL,
                  OpeningCash DECIMAL(18,2) NOT NULL DEFAULT 0,
                  ClosingCash DECIMAL(18,2) NOT NULL DEFAULT 0,
                  TotalSales DECIMAL(18,2) NOT NULL DEFAULT 0,
                  TotalCashSales DECIMAL(18,2) NOT NULL DEFAULT 0,
                  TotalCardSales DECIMAL(18,2) NOT NULL DEFAULT 0,
                  Variance DECIMAL(18,2) NOT NULL DEFAULT 0,
                  Status NVARCHAR(20) NOT NULL DEFAULT 'Open'
              );",

            // v4.2: Discount columns on Transactions for POS discounts
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Transactions') AND name = 'DiscountAmount')
              ALTER TABLE Transactions ADD DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0;",

            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Transactions') AND name = 'DiscountReason')
              ALTER TABLE Transactions ADD DiscountReason NVARCHAR(200) NOT NULL DEFAULT '';",

            // v4.3: SubscriptionPlans table — admin-defined subscription plans
            @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SubscriptionPlans')
            BEGIN
              CREATE TABLE SubscriptionPlans (
                  Id INT IDENTITY(1,1) PRIMARY KEY,
                  NameEn NVARCHAR(200) NOT NULL,
                  NameAr NVARCHAR(200) NOT NULL DEFAULT '',
                  Duration INT NOT NULL DEFAULT 30,
                  DurationType NVARCHAR(20) NOT NULL DEFAULT 'Days',
                  Price DECIMAL(18,2) NOT NULL DEFAULT 0,
                  MaxVisits INT NOT NULL DEFAULT 0,
                  EffectiveTimes INT NOT NULL DEFAULT 65535,
                  IsActive BIT NOT NULL DEFAULT 1,
                  SortOrder INT NOT NULL DEFAULT 0,
                  CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
              );
              CREATE INDEX IX_SubscriptionPlans_IsActive ON SubscriptionPlans(IsActive, SortOrder);
            END",

            // Self-heal: some installs created SubscriptionPlans via EF (or an older schema) which
            // does NOT emit SQL DEFAULT constraints for C# property initializers. On those tables the
            // auto-seed below failed (it omits MaxVisits/EffectiveTimes/CreatedAt) so the table stayed
            // empty ("plans don't appear"), and the Admin "Add plan" INSERT failed with
            // "Cannot insert the value NULL into column 'IsActive'". Add any missing column defaults so
            // every insert path works regardless of how the table was originally created. Idempotent.
            @"IF OBJECT_ID('SubscriptionPlans') IS NOT NULL
            BEGIN
              IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc JOIN sys.columns c ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id WHERE c.object_id=OBJECT_ID('SubscriptionPlans') AND c.name='NameAr')
                ALTER TABLE SubscriptionPlans ADD CONSTRAINT DF_SubscriptionPlans_NameAr DEFAULT '' FOR NameAr;
              IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc JOIN sys.columns c ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id WHERE c.object_id=OBJECT_ID('SubscriptionPlans') AND c.name='Duration')
                ALTER TABLE SubscriptionPlans ADD CONSTRAINT DF_SubscriptionPlans_Duration DEFAULT 30 FOR Duration;
              IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc JOIN sys.columns c ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id WHERE c.object_id=OBJECT_ID('SubscriptionPlans') AND c.name='DurationType')
                ALTER TABLE SubscriptionPlans ADD CONSTRAINT DF_SubscriptionPlans_DurationType DEFAULT 'Days' FOR DurationType;
              IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc JOIN sys.columns c ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id WHERE c.object_id=OBJECT_ID('SubscriptionPlans') AND c.name='Price')
                ALTER TABLE SubscriptionPlans ADD CONSTRAINT DF_SubscriptionPlans_Price DEFAULT 0 FOR Price;
              IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc JOIN sys.columns c ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id WHERE c.object_id=OBJECT_ID('SubscriptionPlans') AND c.name='MaxVisits')
                ALTER TABLE SubscriptionPlans ADD CONSTRAINT DF_SubscriptionPlans_MaxVisits DEFAULT 0 FOR MaxVisits;
              IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc JOIN sys.columns c ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id WHERE c.object_id=OBJECT_ID('SubscriptionPlans') AND c.name='EffectiveTimes')
                ALTER TABLE SubscriptionPlans ADD CONSTRAINT DF_SubscriptionPlans_EffectiveTimes DEFAULT 65535 FOR EffectiveTimes;
              IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc JOIN sys.columns c ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id WHERE c.object_id=OBJECT_ID('SubscriptionPlans') AND c.name='IsActive')
                ALTER TABLE SubscriptionPlans ADD CONSTRAINT DF_SubscriptionPlans_IsActive DEFAULT 1 FOR IsActive;
              IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc JOIN sys.columns c ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id WHERE c.object_id=OBJECT_ID('SubscriptionPlans') AND c.name='SortOrder')
                ALTER TABLE SubscriptionPlans ADD CONSTRAINT DF_SubscriptionPlans_SortOrder DEFAULT 0 FOR SortOrder;
              IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc JOIN sys.columns c ON dc.parent_object_id=c.object_id AND dc.parent_column_id=c.column_id WHERE c.object_id=OBJECT_ID('SubscriptionPlans') AND c.name='CreatedAt')
                ALTER TABLE SubscriptionPlans ADD CONSTRAINT DF_SubscriptionPlans_CreatedAt DEFAULT GETUTCDATE() FOR CreatedAt;
            END",

            // Auto-seed default plans if the table is empty. The Basmia 2026-05-09 deep audit
            // found 842 players using SubscriptionType='Fitness' that didn't exist in the
            // admin's plans table — because the table was empty and the renew dialog
            // defaults to the literal string 'Fitness'. Seeding 5 sensible defaults here
            // means even untouched installs have a usable dropdown, and existing 'Fitness'
            // rows match the seeded 'Fitness' plan name automatically.
            @"IF NOT EXISTS (SELECT 1 FROM SubscriptionPlans)
            BEGIN
              INSERT INTO SubscriptionPlans (NameEn, NameAr, Duration, DurationType, Price, IsActive, SortOrder) VALUES
                ('Fitness',    N'لياقة بدنية',  30, 'Days', 25000, 1, 1),
                ('Cardio',     N'كارديو',       30, 'Days', 20000, 1, 2),
                ('CrossFit',   N'كروس فيت',     30, 'Days', 35000, 1, 3),
                ('Full Access',N'وصول كامل',    30, 'Days', 50000, 1, 4),
                ('Daily Pass', N'بطاقة يومية',   1, 'Days',  3000, 1, 5);
            END",

            // v4.4: Additional indexes for new tables
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_QrPool_Status' AND object_id = OBJECT_ID('QrPool'))
              CREATE INDEX IX_QrPool_Status ON QrPool(Status);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PosShifts_Status' AND object_id = OBJECT_ID('PosShifts'))
              CREATE INDEX IX_PosShifts_Status ON PosShifts(Status);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FreezeHistories_EmployeeId' AND object_id = OBJECT_ID('FreezeHistories'))
              CREATE INDEX IX_FreezeHistories_EmployeeId ON FreezeHistories(EmployeeId);",

            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transactions_CreatedAt_Desc' AND object_id = OBJECT_ID('Transactions'))
              CREATE INDEX IX_Transactions_CreatedAt_Desc ON Transactions(CreatedAt DESC);",

            // v4.5: Delta-sync support — add UpdatedAt columns to mutable tables so the client
            // can send only rows changed since the last successful sync (instead of all rows every time).
            // Triggers keep UpdatedAt in sync automatically so no repository code changes are required.
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'UpdatedAt')
              ALTER TABLE Employees ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();",
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AccessCards') AND name = 'UpdatedAt')
              ALTER TABLE AccessCards ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();",
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('QrPool') AND name = 'UpdatedAt')
              ALTER TABLE QrPool ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();",
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'UpdatedAt')
              ALTER TABLE Users ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();",
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FreezeHistories') AND name = 'UpdatedAt')
              ALTER TABLE FreezeHistories ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();",
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'UpdatedAt')
              ALTER TABLE Products ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();",
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('SubscriptionPlans') AND name = 'UpdatedAt')
              ALTER TABLE SubscriptionPlans ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();",
            @"IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('PosShifts') AND name = 'UpdatedAt')
              ALTER TABLE PosShifts ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();",

            // Triggers: touch UpdatedAt on any UPDATE so the delta-sync query finds changes.
            // CREATE OR ALTER is SQL 2016+; drop-and-recreate pattern works everywhere and is idempotent.
            @"IF OBJECT_ID('TR_Employees_UpdatedAt', 'TR') IS NOT NULL DROP TRIGGER TR_Employees_UpdatedAt;",
            @"EXEC('CREATE TRIGGER TR_Employees_UpdatedAt ON Employees AFTER UPDATE AS
                   BEGIN SET NOCOUNT ON;
                     UPDATE e SET UpdatedAt = GETUTCDATE()
                     FROM Employees e INNER JOIN inserted i ON e.Id = i.Id;
                   END');",

            @"IF OBJECT_ID('TR_AccessCards_UpdatedAt', 'TR') IS NOT NULL DROP TRIGGER TR_AccessCards_UpdatedAt;",
            @"EXEC('CREATE TRIGGER TR_AccessCards_UpdatedAt ON AccessCards AFTER UPDATE AS
                   BEGIN SET NOCOUNT ON;
                     UPDATE a SET UpdatedAt = GETUTCDATE()
                     FROM AccessCards a INNER JOIN inserted i ON a.Id = i.Id;
                   END');",

            @"IF OBJECT_ID('TR_QrPool_UpdatedAt', 'TR') IS NOT NULL DROP TRIGGER TR_QrPool_UpdatedAt;",
            @"EXEC('CREATE TRIGGER TR_QrPool_UpdatedAt ON QrPool AFTER UPDATE AS
                   BEGIN SET NOCOUNT ON;
                     UPDATE q SET UpdatedAt = GETUTCDATE()
                     FROM QrPool q INNER JOIN inserted i ON q.Id = i.Id;
                   END');",

            @"IF OBJECT_ID('TR_Users_UpdatedAt', 'TR') IS NOT NULL DROP TRIGGER TR_Users_UpdatedAt;",
            @"EXEC('CREATE TRIGGER TR_Users_UpdatedAt ON Users AFTER UPDATE AS
                   BEGIN SET NOCOUNT ON;
                     UPDATE u SET UpdatedAt = GETUTCDATE()
                     FROM Users u INNER JOIN inserted i ON u.Id = i.Id;
                   END');",

            @"IF OBJECT_ID('TR_FreezeHistories_UpdatedAt', 'TR') IS NOT NULL DROP TRIGGER TR_FreezeHistories_UpdatedAt;",
            @"EXEC('CREATE TRIGGER TR_FreezeHistories_UpdatedAt ON FreezeHistories AFTER UPDATE AS
                   BEGIN SET NOCOUNT ON;
                     UPDATE f SET UpdatedAt = GETUTCDATE()
                     FROM FreezeHistories f INNER JOIN inserted i ON f.Id = i.Id;
                   END');",

            @"IF OBJECT_ID('TR_Products_UpdatedAt', 'TR') IS NOT NULL DROP TRIGGER TR_Products_UpdatedAt;",
            @"EXEC('CREATE TRIGGER TR_Products_UpdatedAt ON Products AFTER UPDATE AS
                   BEGIN SET NOCOUNT ON;
                     UPDATE p SET UpdatedAt = GETUTCDATE()
                     FROM Products p INNER JOIN inserted i ON p.Id = i.Id;
                   END');",

            @"IF OBJECT_ID('TR_SubscriptionPlans_UpdatedAt', 'TR') IS NOT NULL DROP TRIGGER TR_SubscriptionPlans_UpdatedAt;",
            @"EXEC('CREATE TRIGGER TR_SubscriptionPlans_UpdatedAt ON SubscriptionPlans AFTER UPDATE AS
                   BEGIN SET NOCOUNT ON;
                     UPDATE s SET UpdatedAt = GETUTCDATE()
                     FROM SubscriptionPlans s INNER JOIN inserted i ON s.Id = i.Id;
                   END');",

            @"IF OBJECT_ID('TR_PosShifts_UpdatedAt', 'TR') IS NOT NULL DROP TRIGGER TR_PosShifts_UpdatedAt;",
            @"EXEC('CREATE TRIGGER TR_PosShifts_UpdatedAt ON PosShifts AFTER UPDATE AS
                   BEGIN SET NOCOUNT ON;
                     UPDATE s SET UpdatedAt = GETUTCDATE()
                     FROM PosShifts s INNER JOIN inserted i ON s.Id = i.Id;
                   END');",

            // Index on UpdatedAt for the Players table — hot path for delta sync queries.
            @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Employees_UpdatedAt' AND object_id = OBJECT_ID('Employees'))
              CREATE INDEX IX_Employees_UpdatedAt ON Employees(UpdatedAt);",

            // v4.5: Force SIMPLE recovery model. This product takes twice-daily FULL backups
            // and never takes log backups — so FULL recovery (SQL Server's default when a .bak
            // is restored) grows the transaction log until it fills the disk ("transaction log
            // full due to LOG_BACKUP"), which broke Basmia's backups in May 2026. SIMPLE
            // auto-truncates the log and is correct for this single-PC, full-backup product.
            // Idempotent (only switches when not already SIMPLE) and non-fatal (if the login
            // lacks ALTER permission the loop just logs and continues). EXEC() isolates the
            // ALTER DATABASE into its own batch. Replaces the manual tuneup.sql step so every
            // customer — including new ones — gets it automatically on first launch.
            @"IF (SELECT recovery_model_desc FROM sys.databases WHERE database_id = DB_ID()) <> 'SIMPLE'
              EXEC('ALTER DATABASE CURRENT SET RECOVERY SIMPLE WITH NO_WAIT');",
        };

        var failedMigrations = new List<string>();
        foreach (var sql in migrations)
        {
            try
            {
                db.Database.ExecuteSqlRaw(sql);
            }
            catch (Exception ex)
            {
                // Log but continue — each migration is independent
                failedMigrations.Add($"{sql.Substring(0, Math.Min(sql.Length, 80))}... => {ex.Message}");
            }
        }

        if (failedMigrations.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[DatabaseMigrator] {failedMigrations.Count} migration(s) failed:");
            foreach (var f in failedMigrations)
                System.Diagnostics.Debug.WriteLine($"  - {f}");
        }
    }
}
