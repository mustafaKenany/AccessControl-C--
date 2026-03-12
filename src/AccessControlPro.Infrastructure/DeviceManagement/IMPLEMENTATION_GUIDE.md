# Device Communication Safety - Implementation Guide

## Overview
مجموعة من الـ utilities تضيف safety layers لـ SDK device communication:
- ✅ Rate limiting (prevent DOS on devices)
- ✅ Retry logic مع exponential backoff
- ✅ Input validation (IPs, card permissions, ports)
- ✅ Password encryption for device credentials
- ✅ Device online status monitoring
- ✅ Safe wrapper around SDK calls

---

## 1. Device Password Encryption

**المشكلة الأصلية:**
```csharp
public string Password { get; set; } = "FFFFFFFF";  // ❌ Plaintext in DB
```

**الحل:**
```csharp
// في Device entity:
public string Password { get; set; } = "FFFFFFFF";  // Still stored as is initially

// عند الاستخدام:
var passwordEncryption = serviceProvider.GetRequiredService<IDevicePasswordEncryption>();
var encryptedPwd = passwordEncryption.EncryptPassword(device.Password);
// Store encryptedPwd in DB instead of plaintext

// عند استخدام الـ SDK:
var decryptedPwd = passwordEncryption.DecryptPassword(storedEncryptedPassword);
var deviceInfo = new DeviceInfo { Password = decryptedPwd, ... };
```

**Migration Plan:**
1. إضافة عمود جديد `EncryptedPassword` للـ Device table
2. Migrate existing passwords: encrypt + حفظ في العمود الجديد
3. Update Device.cs لاستخدام EncryptedPassword
4. حذف العمود القديم Password

---

## 2. Device Parameter Validation

**الاستخدام:**
```csharp
var validator = serviceProvider.GetRequiredService<IDeviceParameterValidator>();

// Validate IP
var (isValid, error) = validator.ValidateIPAddress("192.168.1.100");
if (!isValid) throw new InvalidOperationException(error);

// Validate Subnet
var (maskValid, maskError) = validator.ValidateSubnetMask("255.255.255.0");

// Comprehensive check
var (allValid, errors) = validator.ValidateDeviceNetworkConfig(
    "192.168.1.100",  // IP
    8000,             // TCP Port
    8101,             // UDP Port
    "192.168.1.1",    // Gateway
    "255.255.255.0",  // Subnet Mask
    "AA:BB:CC:DD:EE:FF" // MAC
);

if (!allValid)
    throw new InvalidOperationException(string.Join("; ", errors));
```

**يحارب:**
- Invalid IP addresses (999.999.999.999)
- Invalid ports (< 1024 or > 65535)
- Invalid subnet masks
- Invalid gateway addresses

---

## 3. Rate Limiting for Card Operations

**المشكلة الأصلية:**
```csharp
// ❌ Can add 100,000 cards instantly = DOS
for (int i = 0; i < 100000; i++)
    await employeeService.AssignCardAsync(deviceId, card);
```

**الحل:**
```csharp
var rateLimiter = serviceProvider.GetRequiredService<ICardOperationRateLimiter>();

// في DeviceService.cs AddAccessCard method:
public async Task<bool> AddAccessCardSafeAsync(/*...params...*/)
{
    try
    {
        // Throws InvalidOperationException if rate limit exceeded
        rateLimiter.ThrottleOrThrow(deviceId, "CardSync");

        // ... perform operation ...

        // Record successful operation
        rateLimiter.RecordOperation(deviceId, "CardSync");

        return true;
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit"))
    {
        // Handle rate limit - inform user to retry later
        throw;
    }
}
```

**Configuration:**
- Default: 100 card operations per 60 seconds per device
- Configurable: `new CardOperationRateLimiter(maxOps: 50, windowSeconds: 30)`

---

## 4. SDK Retry Policy with Exponential Backoff

**المشكلة الأصلية:**
```csharp
// ❌ Network timeout = immediate failure
_sdk.AddAccessCard(device, cardNo, ...);  // Crashes if timeout
```

**الحل:**
```csharp
var retryPolicy = serviceProvider.GetRequiredService<ISdkRetryPolicy>();

// Automatic retry with backoff
await retryPolicy.ExecuteWithRetryAsync(
    async () =>
    {
        _sdk.AddAccessCard(device, cardNo, ...);
        return true;
    },
    operationName: "AddCard(123 -> Device.IP)",
    maxRetries: 3,
    initialDelayMs: 500
    // First attempt: immediate
    // Retry 1: 500ms delay
    // Retry 2: 750ms delay (1.5x backoff)
    // Retry 3: 1125ms delay
);
```

**مثال لـ Fire-and-Forget:**
```csharp
// Background operation, don't throw
await retryPolicy.ExecuteFireAndForgetAsync(
    async () =>
    {
        await DisableCardsOnAllDevices(employee);
    },
    operationName: "DisableCards",
    maxRetries: 2,
    onFailure: ex => LogError($"Failed to disable cards: {ex.Message}")
);
```

---

## 5. Card Door Permissions Validation

**المشكلة الأصلية:**
```csharp
// ❌ No validation
var card = new AccessCard { DoorPermissions = "INVALID123" };
// SDK crashes silently
```

**الحل:**
```csharp
var cardValidator = serviceProvider.GetRequiredService<ICardDoorPermissionsValidator>();

// Validate individual parameters
var (permValid, permError) = cardValidator.ValidateDoorPermissions("01000000");
if (!permValid) throw new InvalidOperationException(permError);

var (timesValid, timesError) = cardValidator.ValidateEffectiveTimes(5);
if (!timesValid) throw new InvalidOperationException(timesError);

// Comprehensive validation
var (allValid, errors) = cardValidator.ValidateCardParameters(
    cardNumber: "12345",
    doorPermissions: "01000000",  // "0"=door closed, "1"=door allowed
    effectiveTimes: 100,          // 0=unlimited, 1-65535=usage count
    openMode: 1,                  // 0-3
    validFrom: DateTime.Today,
    validTo: DateTime.Today.AddMonths(1)
);

if (!allValid)
    throw new InvalidOperationException(string.Join("; ", errors));
```

---

## 6. Device Status Monitor

**المشكلة الأصلية:**
```csharp
// ❌ Device shows Online but network disconnected
if (device.IsOnline)  // Always returns true
    _sdk.RemoteOpenDoor(device, ...);  // Timeout!
```

**الحل:**
```csharp
var statusMonitor = serviceProvider.GetRequiredService<IDeviceStatusMonitor>();

// Start background monitoring
var devices = await deviceRepository.GetAllAsync();
statusMonitor.StartMonitoring(
    devices.Select(d => new DeviceInfo { IP = d.IP, ... }).ToList(),
    checkIntervalSeconds: 30
);

// Manual check
bool isOnline = await statusMonitor.IsDeviceOnlineAsync(deviceInfo, timeoutMs: 5000);
if (!isOnline)
    throw new InvalidOperationException("Device is currently offline");

// Get all device statuses
var statuses = statusMonitor.GetDeviceStatuses();
foreach (var (deviceId, (isOnline, lastChecked)) in statuses)
{
    Console.WriteLine($"Device {deviceId}: {(isOnline ? "ONLINE" : "OFFLINE")} (checked {lastChecked})");
}
```

---

## 7. Safe Device Communication (Complete Wrapper)

**The All-in-One Safe Wrapper:**
```csharp
var safeCom = serviceProvider.GetRequiredService<ISafeDeviceCommunication>();

// ✅ Uses all safety features: validation + rate limit + retry
await safeCom.AddAccessCardSafeAsync(
    device: deviceInfo,
    cardNumber: "12345",
    cardPassword: "pwd",
    openMode: 1,
    doorPermissions: "01000000",
    permitTime: DateTime.Today.AddMonths(1),
    effectiveTimes: 100
);
// Validates all params ✓
// Checks rate limit ✓
// Retries on timeout ✓
// Logs everything ✓

// Other safe methods:
await safeCom.UpdateIPAddressSafeAsync(device, "192.168.1.50");
await safeCom.RemoteOpenDoorSafeAsync(device, new[] { 1, 2 });
var info = await safeCom.GetDeviceInfoSafeAsync(device);
await safeCom.CalibrateTimeSafeAsync(device);
```

---

## Integration Checklist

### Phase 1: Register Dependencies ✅
- [ ] Update `DependencyInjection.cs` (DONE)
- [ ] Compile project successfully

### Phase 2: Start Using in Services
- [ ] DeviceService: Inject `ISafeDeviceCommunication`, use for all SDK calls
- [ ] EmployeeService: Use `ISafeDeviceCommunication` for card sync operations
- [ ] Update card operations in `SyncCardToDeviceAsync()` method

### Phase 3: Encrypt Device Passwords
- [ ] Create EF migration to add `EncryptedPassword` column
- [ ] Migrate data: encrypt existing plaintext passwords
- [ ] Update Device model to use encrypted field
- [ ] Update DeviceService to use encryption

### Phase 4: Enable Monitoring
- [ ] App startup: Start `IDeviceStatusMonitor`
- [ ] MainViewModel: Display device status in UI
- [ ] Admin: Show "Online/Offline" for each device

---

## Testing Examples

```csharp
// Unit Test: Rate Limiter
[Fact]
public void RateLimiter_ThrowsWhenExceeded()
{
    var limiter = new CardOperationRateLimiter(maxOperationsPerMinute: 3);
    limiter.ThrottleOrThrow(1); // OK
    limiter.ThrottleOrThrow(1); // OK
    limiter.ThrottleOrThrow(1); // OK
    Assert.Throws<InvalidOperationException>(() =>
        limiter.ThrottleOrThrow(1));  // Throws!
}

// Unit Test: IP Validation
[Fact]
public void Validator_RejectsInvalidIP()
{
    var validator = new DeviceParameterValidator();
    var (isValid, error) = validator.ValidateIPAddress("999.999.999.999");
    Assert.False(isValid);
    Assert.NotNull(error);
}

// Unit Test: Card Permissions
[Fact]
public void CardValidator_ValidatesPermissions()
{
    var validator = new CardDoorPermissionsValidator();
    var (valid, error) = validator.ValidateDoorPermissions("01000000");
    Assert.True(valid);

    var (invalid, error2) = validator.ValidateDoorPermissions("INVALID");
    Assert.False(invalid);
}
```

---

## Migration Steps Summary

1. ✅ Created all utility classes
2. ✅ Registered in DependencyInjection
3. **Next**: Update DeviceService to inject `ISafeDeviceCommunication`
4. **Next**: Update EmployeeService card sync methods
5. **Next**: Create migration for password encryption
6. **Next**: Start device monitor in App.xaml.cs

---

## Production Readiness Checklist

- [ ] All SDK calls go through `ISafeDeviceCommunication`
- [ ] Device passwords encrypted in transit
- [ ] Rate limiting prevents DOS attacks
- [ ] Device online status monitored continuously
- [ ] All parameters validated before SDK calls
- [ ] Retry logic handles network timeouts
- [ ] Proper error logging with TraceSource
- [ ] Unit tests for each validator
- [ ] Integration tests for SD communication
- [ ] Load test: verify rate limiter under high load
