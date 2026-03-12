/*
// ============================================================================
// EXAMPLE USAGE: How to integrate SafeDeviceCommunication into existing Services
//
// NOTE: This file contains code examples only. It's not meant to compile.
// Use these examples as a guide when updating DeviceService and EmployeeService.
// ============================================================================

EXAMPLE 1: DeviceService - Integrating SafeDeviceCommunication
=====================================================================

    public class DeviceServiceExample : IDeviceService
    {
        private readonly ISafeDeviceCommunication _safeCom;  // ✨ NEW
        private readonly IDeviceParameterValidator _validator;  // ✨ NEW

        public async Task<bool> UpdateIPAsync(int deviceId, string newIP, string newGateway, string subnetMask)
        {
            // ✨ VALIDATE before doing anything
            var (isValid, errors) = _validator.ValidateDeviceNetworkConfig(
                newIP, device.TCPPort, device.UDPPort, newGateway, subnetMask, device.MAC);

            if (!isValid)
                throw new InvalidOperationException($"Invalid network config: {string.Join("; ", errors)}");

            // ✨ Use SafeDeviceCommunication (handles retry + validation internally too)
            await _safeCom.UpdateIPAddressSafeAsync(deviceInfo, newIP);

            // Update DB after successful SDK call
            device.IP = newIP;
            await _deviceRepository.UpdateAsync(device);
            return true;
        }
    }


EXAMPLE 2: EmployeeService - SyncCardToDeviceAsync with SafeDeviceCommunication
=====================================================================

    public class EmployeeServiceExample
    {
        private readonly ISafeDeviceCommunication _safeCom;  // ✨ NEW
        private readonly ICardDoorPermissionsValidator _cardValidator;  // ✨ NEW
        private readonly ICardOperationRateLimiter _rateLimiter;  // ✨ NEW

        public async Task<bool> SyncCardToDeviceAsync(int cardId, int deviceId, Device device, AccessCard card)
        {
            // ✨ VALIDATE card parameters before SDK call
            var (cardValid, cardErrors) = _cardValidator.ValidateCardParameters(
                card.CardNumber, card.DoorPermissions, card.EffectiveTimes,
                card.OpenMode, card.ValidFrom, card.ValidTo);

            if (!cardValid)
                throw new InvalidOperationException($"Invalid card parameters: {string.Join("; ", cardErrors)}");

            try
            {
                // ✨ Use SafeDeviceCommunication instead of direct SDK
                // This adds: validation, rate limiting, retry logic, error handling
                await _safeCom.AddAccessCardSafeAsync(
                    deviceInfo,
                    card.CardNumber,
                    card.CardPassword,
                    card.OpenMode,
                    card.DoorPermissions,
                    card.ValidTo,
                    card.EffectiveTimes,
                    card.TimePeriodIndex,
                    card.HolidayEnabled);

                return true;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit"))
            {
                // Rate limit hit - user should retry later
                throw new InvalidOperationException(
                    $"Too many card operations on device. Please wait a moment and try again.", ex);
            }
        }
    }


EXAMPLE 3: Using Rate Limiter in a loop
=====================================================================

    foreach (var (cardId, deviceId) in cardOperations)
    {
        try
        {
            // Check rate limit before attempting
            rateLimiter.ThrottleOrThrow(deviceId, "CardSync");

            // Perform operation
            await safeCom.AddAccessCardSafeAsync(...);

            // Record if successful
            rateLimiter.RecordOperation(deviceId, "CardSync");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit"))
        {
            // Extract retry time and wait
            var retryAfter = ExtractRetryAfterSeconds(ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(retryAfter));

            // Retry after reset
            try
            {
                rateLimiter.ThrottleOrThrow(deviceId, "CardSync");
                await safeCom.AddAccessCardSafeAsync(...);
                rateLimiter.RecordOperation(deviceId, "CardSync");
            }
            catch
            {
                Console.WriteLine("Failed even after retry");
            }
        }
    }


EXAMPLE 4: DeviceStatusMonitor Usage in App startup
=====================================================================

    public async void OnAppInitialize(IDeviceStatusMonitor statusMonitor, IDeviceRepository deviceRepository)
    {
        // ✨ Start monitoring all devices
        var allDevices = await deviceRepository.GetAllAsync();
        var deviceInfos = allDevices.Select(d => new DeviceInfo
        {
            IP = d.IP,
            MAC = d.MAC,
            SerialNumber = d.SerialNumber,
            TCPPort = d.TCPPort,
            Password = d.Password,  // TODO: Decrypt if encrypted
            Gateway = d.Gateway,
            SubnetMask = d.SubnetMask
        }).ToList();

        statusMonitor.StartMonitoring(deviceInfos, checkIntervalSeconds: 30);
        Console.WriteLine($"Device status monitoring started for {deviceInfos.Count} devices");
    }

    public void OnAppShutdown(IDeviceStatusMonitor statusMonitor)
    {
        statusMonitor.StopMonitoring();
    }


INTEGRATION STEPS
=====================================================================

1. Inject SafeDeviceCommunication into DeviceService:
   - Add private field: ISafeDeviceCommunication _safeCom
   - Add to constructor params
   - Replace all _sdk.AddAccessCard calls with _safeCom.AddAccessCardSafeAsync

2. Inject validators into EmployeeService:
   - Add ICardDoorPermissionsValidator _cardValidator
   - Add ICardOperationRateLimiter _rateLimiter
   - Add validation before each SDK call

3. Start device monitoring in App startup
   - In MainWindow.xaml.cs or App.xaml.cs
   - Call statusMonitor.StartMonitoring(...) on app init
   - Call statusMonitor.StopMonitoring() on app shutdown

4. Migrate device passwords to encrypted storage
   - Create migration: Add EncryptedPassword column
   - Migrate existing passwords: Encrypt + store in new column
   - Update Device entity to use encrypted field
   - Update DeviceService to decrypt on read


KEY BENEFITS
=====================================================================

✅ Rate Limiting: Prevents DOS attacks on devices (100 ops/min default)
✅ Retry Logic: Automatic retry with exponential backoff (500ms -> 750ms -> 1125ms)
✅ Validation: All device IPs, ports, card permissions validated before SDK call
✅ Encryption: Device passwords encrypted at rest using DPAPI
✅ Monitoring: Continuous background ping to detect offline devices
✅ Safe Wrapper: All SDK communication goes through one validated interface
✅ Error Handling: Comprehensive error messages with actionable guidance


MIGRATION CHECKLIST
=====================================================================

- [ ] All utilities created and registered in DependencyInjection ✅ DONE
- [ ] DeviceService updated to inject ISafeDeviceCommunication
- [ ] EmployeeService updated to use validators and rate limiter
- [ ] Device password encryption implemented
- [ ] Device status monitor started on app init
- [ ] Unit tests for validators
- [ ] Integration tests for SafeDeviceCommunication
- [ ] Load tests: verify rate limiter under high load (1000+ ops/min)
- [ ] Production deployment of SDK safety wrapper

*/
