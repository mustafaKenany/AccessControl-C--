-- Adds a QrPass record so the test code appears on the QR Daily Pass page in the app.
-- Run AFTER insert_test_pool_code.sql.
-- The QrPass references the same code (10000001) that's already in the pool + uploaded to the device.

USE AccessControlPro;
GO

IF NOT EXISTS (SELECT 1 FROM QrPasses WHERE PassCode = '10000001')
BEGIN
    -- Get a real device id from the customer's Devices table so the foreign key resolves
    DECLARE @deviceId INT = (SELECT TOP 1 Id FROM Devices ORDER BY Id);

    INSERT INTO QrPasses (
        PassCode,
        PlayerName,
        Phone,
        ValidFrom,
        ValidTo,
        MaxUses,
        UsedCount,
        Fee,
        IsActive,
        CreatedBy,
        CreatedAt,
        DeviceId,
        DoorNumber,
        DeviceName
    )
    VALUES (
        '10000001',
        'WG34_TEST',
        '',
        GETUTCDATE(),
        DATEADD(DAY, 30, GETUTCDATE()),
        2,
        0,
        0,
        1,
        'admin',
        GETUTCDATE(),
        @deviceId,
        1,
        ''
    );

    PRINT 'QrPass record inserted for code 10000001';
END
ELSE
BEGIN
    PRINT 'QrPass already exists — skipping';
END

SELECT PassCode, PlayerName, ValidTo, IsActive, DeviceId
FROM QrPasses
WHERE PassCode = '10000001';
GO
