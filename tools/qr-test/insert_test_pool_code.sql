-- =============================================================================
-- QR Pool 24-bit fit test
-- =============================================================================
-- Purpose: insert ONE pool code (10000001) that fits in WG34's 24-bit card
-- field. If the device shows "Card = 10000001" (or close to it) when its QR
-- is scanned, our theory is right and we can change the pool range globally.
--
-- Compare: existing pool codes start at 50001001, which OVERFLOWS 24 bits.
-- The QR-reader-to-controller WG34 transmission was putting the high byte
-- into the facility code and only the high byte (0x02 for 50001695) was
-- being shown by the controller as "Card = 2".
--
-- A 7-digit code like 10000001 is below 16,777,215 (24-bit max), so the
-- facility byte is 0 and the full number reaches the controller intact.
--
-- Run this against the LOCAL gym database (not the cloud).
-- =============================================================================

USE AccessControlPro;
GO

-- Safety: don't insert twice if you re-run the script
IF NOT EXISTS (SELECT 1 FROM QrPool WHERE Code = '10000001')
BEGIN
    INSERT INTO QrPool (
        Code,
        Status,                   -- 1 = Assigned (ready to display + scan)
        Source,
        GuestName,
        GuestPhone,
        Reason,
        AssignedAt,
        MaxUses,
        UsedCount,
        ValidFrom,
        ValidTo,
        DoorPermissions,          -- 01010101 = all 4 doors allowed; matches your existing entries
        CreatedAt,
        IsUploadedToDevice        -- 0 forces the next QR Pool sync to push it to the controller
    )
    VALUES (
        '10000001',
        1,
        'Local',
        'WG34_TEST',              -- easy to find in the QR Daily Pass list
        '',
        'Pool range fit test — see if device reads full 7-digit code',
        GETUTCDATE(),
        2,
        0,
        GETUTCDATE(),
        DATEADD(DAY, 30, GETUTCDATE()),
        '01010101',
        GETUTCDATE(),
        0
    );

    PRINT 'Test pool entry inserted: code=10000001 guest=WG34_TEST';
END
ELSE
BEGIN
    PRINT 'Test pool entry already exists — skipping insert';
END

-- Show the row so you can confirm
SELECT Code, Status, GuestName, ValidTo, IsUploadedToDevice
FROM QrPool
WHERE Code = '10000001';
GO
