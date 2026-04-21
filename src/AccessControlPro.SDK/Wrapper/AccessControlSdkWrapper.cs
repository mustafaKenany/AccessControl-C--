using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Native;
using FCardCDrive;
using FCardCDrive.Connect;

namespace AccessControlPro.SDK.Wrapper;

public class AccessControlSdkWrapper : IAccessControlSdk
{
    private bool _initialized;
    private ConnectMain? _connectMain;
    private ConnectMain? _monitorMain;
    private System.Timers.Timer? _keepAliveTimer;
    private List<DeviceInfo>? _monitoredDevices;
    private Action<MonitorEvent>? _onMonitorEvent;
    private readonly object _monitorLock = new();
    private readonly object _initLock = new();
    private static readonly string SdkLogPath = Path.Combine(AppContext.BaseDirectory, "sdk_log.txt");

    private static void SdkLog(string msg)
    {
        SdkLogFile.Append(SdkLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}\n");
    }

    private static string[] BuildDeviceParams(DeviceInfo device)
    {
        // Must match demo exactly: { "s", ip, tcpport, sn, password, null*11, mac, subnet, gateway, ... }
        // The native code checks for null pointers on indices 5-15 and skips them.
        // Passing "" (empty string) instead of null causes the native code to try parsing
        // empty strings as IPs → CString Find(".") returns -1 → iStart >= 0 assertion failure.
        return new string[]
        {
            "s",                                                                    // [0]
            device.IP,                                                              // [1]
            device.TCPPort.ToString(),                                              // [2]
            device.SerialNumber,                                                    // [3]
            device.Password,                                                        // [4]
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, // [5-15]
            device.MAC,                                                             // [16]
            string.IsNullOrEmpty(device.SubnetMask) ? "255.255.255.0" : device.SubnetMask, // [17]
            string.IsNullOrEmpty(device.Gateway) ? "0.0.0.0" : device.Gateway,     // [18]
            "0.0.0.0",                                                              // [19]
            "0.0.0.0",                                                              // [20]
            "server",                                                               // [21]
            device.TCPPort.ToString(),                                              // [22]
            device.UDPPort.ToString()                                               // [23]
        };
    }

    public void Initialize()
    {
        lock (_initLock)
        {
            if (!_initialized)
            {
                try
                {
                    SdkLog("Initialize: PreloadNativeLibraries...");
                    CareaIfcNative.PreloadNativeLibraries();
                    SdkLog("Initialize: initNet...");
                    CareaIfcNative.initNet("AccessControlPro");
                    SdkLog("Initialize: Creating ConnectMain...");
                    _connectMain = new ConnectMain();
                    _initialized = true;
                    SdkLog("Initialize: SUCCESS");
                }
                catch (Exception ex)
                {
                    SdkLog($"Initialize: FAILED - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                    throw;
                }
            }
        }
    }

    public void Shutdown()
    {
        lock (_initLock)
        {
            if (_initialized)
            {
                CareaIfcNative.clearNet();
                _connectMain = null;
                _initialized = false;
            }
        }
    }

    public async Task<DeviceInfo?> SearchDeviceAsync(int timeoutMs = 20000)
    {
        SdkLog("=== SearchDeviceAsync START ===");

        if (_connectMain == null)
        {
            SdkLog("SearchDeviceAsync: _connectMain is NULL, aborting");
            return null;
        }

        var tcs = new TaskCompletionSource<DeviceInfo?>();
        var rand = new Random();
        long netNum = rand.Next(1, 65535);
        int searchAttempt = 0;
        const int maxAttempts = 3;

        void OnCommandAchieve(ConnectInfo oInfo, int iCommandCode, object oValue)
        {
            try
            {
                SdkLog($"CommandAchieve: code={iCommandCode}");
                if (iCommandCode != (int)ConnectMain.eCommandCode.cmdSearchEquptOnNetNum) return;

                var tcp = GetPropertyValue<object>(oValue, "TCP");
                var sn = GetPropertyValue<string>(oValue, "SN") ?? "";
                SdkLog($"Device found! SN={sn}");

                if (tcp == null)
                {
                    SdkLog("TCP object is null — waiting for more results");
                    return;
                }

                var ip = GetPropertyValue<string>(tcp, "IP") ?? "";
                var mac = GetPropertyValue<string>(tcp, "MAC") ?? "";
                var tcpPort = GetIntPropertyValue(tcp, "TCPPort");
                var udpPort = GetIntPropertyValue(tcp, "UDPPort");
                var gateway = GetPropertyValue<string>(tcp, "IPGateway") ?? "";
                var subnetMask = GetPropertyValue<string>(tcp, "Mask") ?? "255.255.255.0";

                SdkLog($"IP={ip}, MAC={mac}, TCP={tcpPort}, UDP={udpPort}, GW={gateway}, SM={subnetMask}");

                tcs.TrySetResult(new DeviceInfo
                {
                    IP = ip,
                    MAC = mac,
                    SerialNumber = sn,
                    TCPPort = tcpPort != 0 ? tcpPort : 8000,
                    UDPPort = udpPort != 0 ? udpPort : 8101,
                    Gateway = gateway,
                    SubnetMask = subnetMask,
                    Password = "FFFFFFFF"
                });
            }
            catch (Exception ex)
            {
                SdkLog($"CommandAchieve ERROR: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        void OnCommandTimeout(ConnectInfo oInfo, int iCommandCode, int iStep, object oValue)
        {
            try
            {
                SdkLog($"CommandTimeout: code={iCommandCode}, step={iStep}, attempt={searchAttempt}/{maxAttempts}");
                if (iCommandCode != (int)ConnectMain.eCommandCode.cmdSearchEquptOnNetNum) return;

                searchAttempt++;
                if (searchAttempt < maxAttempts && !tcs.Task.IsCompleted)
                {
                    SdkLog($"Retrying search (attempt {searchAttempt + 1})...");
                    var retryInfo = BuildSearchConnectInfo(0, 5000);
                    _connectMain!.Command(retryInfo, "SearchEquptOnNetNum", netNum, 1);
                    SdkLog("Retry search command sent via Command()");
                }
                else
                {
                    SdkLog("Max attempts reached, giving up");
                    tcs.TrySetResult(null);
                }
            }
            catch (Exception ex)
            {
                SdkLog($"CommandTimeout ERROR: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                tcs.TrySetResult(null);
            }
        }

        void OnConnectError(ConnectInfo oInfo, int iCommandCode, object oValue)
        {
            try
            {
                SdkLog($"ConnectError: code={iCommandCode}, value={oValue}");
                if (iCommandCode == (int)ConnectMain.eCommandCode.cmdSearchEquptOnNetNum)
                {
                    searchAttempt = maxAttempts;
                    tcs.TrySetResult(null);
                }
            }
            catch (Exception ex)
            {
                SdkLog($"ConnectError ERROR: {ex.GetType().Name}: {ex.Message}");
                tcs.TrySetResult(null);
            }
        }

        void OnPasswordError(ConnectInfo oInfo, int iCommandCode, object oValue)
        {
            try
            {
                SdkLog($"PasswordError: code={iCommandCode}");
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                SdkLog($"PasswordError ERROR: {ex.Message}");
            }
        }

        _connectMain.CommandAchieve += OnCommandAchieve;
        _connectMain.CommandTimeout += OnCommandTimeout;
        _connectMain.ConnectError += OnConnectError;
        _connectMain.PasswordError += OnPasswordError;

        try
        {
            // Use Command() method exactly like the demo does (not SearchEquptOnNetNum method)
            var oInfo = BuildSearchConnectInfo(0, 5000);

            SdkLog($"Calling Command('SearchEquptOnNetNum'): netNum={netNum}");
            var result = _connectMain.Command(oInfo, "SearchEquptOnNetNum", netNum, 1);
            SdkLog($"Command returned: {result}");

            using var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() =>
            {
                SdkLog("Overall timeout reached");
                tcs.TrySetResult(null);
            });

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            SdkLog($"SearchDeviceAsync EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            if (ex.InnerException != null)
                SdkLog($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}");
            return null;
        }
        finally
        {
            _connectMain.CommandAchieve -= OnCommandAchieve;
            _connectMain.CommandTimeout -= OnCommandTimeout;
            _connectMain.ConnectError -= OnConnectError;
            _connectMain.PasswordError -= OnPasswordError;
            SdkLog("=== SearchDeviceAsync END ===");
        }
    }

    /// <summary>
    /// Build ConnectInfo for search — matches demo exactly
    /// </summary>
    private static ConnectInfo BuildSearchConnectInfo(int restartCount = 1, int timeoutMs = 1500)
    {
        return new ConnectInfo
        {
            ConnType = ConnectInfo.e_ConnectType.OnUDP,
            IP = "",
            NetPort = 8101,
            UDPBroadcast = true,
            EquptType = ConnectInfo.e_EquptType.FC8900,
            SN = "MC-5824T25070244",
            Password = "FFFFFFFF",
            RestartCount = restartCount,
            TimeOutMSEL = timeoutMs
        };
    }

    private static T? GetPropertyValue<T>(object obj, string propertyName)
    {
        var prop = obj.GetType().GetProperty(propertyName);
        if (prop == null)
        {
            var field = obj.GetType().GetField(propertyName);
            if (field != null) return (T?)field.GetValue(obj);
            return default;
        }
        return (T?)prop.GetValue(obj);
    }

    private static int GetIntPropertyValue(object obj, string propertyName)
    {
        var prop = obj.GetType().GetProperty(propertyName);
        object? val = null;
        if (prop != null)
            val = prop.GetValue(obj);
        else
        {
            var field = obj.GetType().GetField(propertyName);
            if (field != null) val = field.GetValue(obj);
        }
        return val != null ? Convert.ToInt32(val) : 0;
    }

    public void InitializeDevice(DeviceInfo device, int doorCount)
    {
        var s = BuildDeviceParams(device);

        var logPath = Path.Combine(AppContext.BaseDirectory, "install_log.txt");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== install called at {DateTime.Now} ===");
        sb.AppendLine($"doorCount={doorCount}");
        for (int i = 0; i < s.Length; i++)
            sb.AppendLine($"  s[{i}] = {(s[i] == null ? "NULL" : $"\"{s[i]}\"")}");
        sb.AppendLine("About to call native install()...");
        File.WriteAllText(logPath, sb.ToString());

        try
        {
            var result = CareaIfcNative.install(true, s, doorCount);
            File.AppendAllText(logPath, $"install() returned: {result}\n");
            if (result == IntPtr.Zero)
                SdkLog($"install() WARNING: returned zero (possible failure) for device {device.SerialNumber}");
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"install() EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n");
            throw;
        }
    }

    public string ReadDeviceTime(DeviceInfo device)
    {
        var s = BuildDeviceParams(device);
        var ptr = CareaIfcNative.readDevNowTime(s, "");
        return ptr != IntPtr.Zero ? Marshal.PtrToStringUni(ptr) ?? string.Empty : string.Empty;
    }

    public void CalibrateTime(DeviceInfo device)
    {
        var s = BuildDeviceParams(device);
        var result = CareaIfcNative.calibrationTime(s);
        SdkLog($"calibrationTime() returned: {result} for device {device.SerialNumber}");
    }

    public void UpdateIP(DeviceInfo device, int doorCount)
    {
        // Must match demo exactly: { mac, ip, tcpport, sn, password, null*11, mac, subnet, gateway,
        //   "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, doorCount }
        // The native code checks for null pointers on indices 5-15, 27, 28 and skips them.
        var s = new string[]
        {
            device.MAC,                                                             // [0]
            device.IP,                                                              // [1]
            device.TCPPort.ToString(),                                              // [2]
            device.SerialNumber,                                                    // [3]
            device.Password,                                                        // [4]
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, // [5-15]
            device.MAC,                                                             // [16]
            string.IsNullOrEmpty(device.SubnetMask) ? "255.255.255.0" : device.SubnetMask, // [17]
            string.IsNullOrEmpty(device.Gateway) ? "0.0.0.0" : device.Gateway,     // [18]
            "0.0.0.0",                                                              // [19]
            "0.0.0.0",                                                              // [20]
            "server",                                                               // [21]
            device.TCPPort.ToString(),                                              // [22]
            device.UDPPort.ToString(),                                              // [23]
            "0.0.0.0",                                                              // [24]
            "",                                                                     // [25]
            "9010",                                                                 // [26]
            null!,                                                                  // [27]
            null!,                                                                  // [28]
            doorCount.ToString()                                                    // [29]
        };

        // Debug: log all values before native call
        var logPath = Path.Combine(AppContext.BaseDirectory, "updateip_log.txt");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== UpdateIP called at {DateTime.Now} ===");
        sb.AppendLine($"doorCount={doorCount}");
        for (int i = 0; i < s.Length; i++)
            sb.AppendLine($"  s[{i}] = {(s[i] == null ? "NULL" : $"\"{s[i]}\"")}");
        File.WriteAllText(logPath, sb.ToString());

        var result = CareaIfcNative.updateIP(s);
        File.AppendAllText(logPath, $"updateIP() returned: {result}\n");
        SdkLog($"updateIP() returned: {result} for device {device.SerialNumber}");
    }

    public void RemoteOpenDoor(DeviceInfo device, int[] doorNumbers)
    {
        Initialize(); // Ensure SDK is ready
        if (_connectMain == null)
            throw new InvalidOperationException("SDK not initialized - ConnectMain is null");
        var info = BuildConnectInfo(device);
        var doors = new bool[4];
        foreach (var d in doorNumbers)
            if (d >= 0 && d < 4) doors[d] = true;
        SdkLog($"RemoteOpenDoor: device={device.SerialNumber} doors=[{string.Join(",", doorNumbers)}]");
        var result = _connectMain.Command(info, "OpenRelay", doors);
        SdkLog($"RemoteOpenDoor result: {result}");
    }

    public void RemoteCloseDoor(DeviceInfo device, int[] doorNumbers)
    {
        Initialize(); // Ensure SDK is ready
        if (_connectMain == null)
            throw new InvalidOperationException("SDK not initialized - ConnectMain is null");
        var info = BuildConnectInfo(device);
        var doors = new bool[4];
        foreach (var d in doorNumbers)
            if (d >= 0 && d < 4) doors[d] = true;
        SdkLog($"RemoteCloseDoor: device={device.SerialNumber} doors=[{string.Join(",", doorNumbers)}]");
        var result = _connectMain.Command(info, "CloseRelay", doors);
        SdkLog($"RemoteCloseDoor result: {result}");
    }

    private static FCardCDrive.Connect.ConnectInfo BuildConnectInfo(DeviceInfo device)
    {
        return new FCardCDrive.Connect.ConnectInfo
        {
            SN = device.SerialNumber,
            IP = device.IP,
            NetPort = (ushort)device.TCPPort,
            Password = device.Password,
            EquptType = FCardCDrive.Connect.ConnectInfo.e_EquptType.FC8900,
            ConnType = FCardCDrive.Connect.ConnectInfo.e_ConnectType.OnTCPClient,
            RestartCount = 3,
            TimeOutMSEL = 6000
        };
    }

    public void SetDoorDelay(DeviceInfo device, int doorNumber, int delaySeconds)
    {
        if (doorNumber < 0 || doorNumber > 3)
            throw new ArgumentOutOfRangeException(nameof(doorNumber), "Door number must be 0-3");
        var s = BuildDeviceParams(device);
        // Door selection array: "1"/"2"/"3"/"4" for selected, "0" for unselected
        var doorSelect = new[] { "0", "0", "0", "0" };
        doorSelect[doorNumber] = (doorNumber + 1).ToString();
        var result = CareaIfcNative.openTimeDelay(s, 3, doorSelect, delaySeconds.ToString().PadLeft(4, '0'));
        SdkLog($"openTimeDelay() returned: {result} for device {device.SerialNumber}, door {doorNumber}, delay {delaySeconds}s");
    }

    public void SetOpeningHours(DeviceInfo device, int timeNum, string timePieces)
    {
        var s = BuildDeviceParams(device);
        var result = CareaIfcNative.setTimes(s, 1, timeNum, timePieces);
        SdkLog($"setTimes() returned: {result} for device {device.SerialNumber}");
    }

    public void AddAccessCard(DeviceInfo device, string cardNo, string cardPassword, int openMode, string openLock, string permitTime, int effectiveTimes = 1, int timePeriodIndex = 0, bool holidayEnabled = false)
    {
        Initialize(); // Ensure SDK is ready

        // Sanitize: never send date before 2024 — use 10 years from now instead
        if (DateTime.TryParse(permitTime, out var parsedDate) && parsedDate.Year < 2024)
            permitTime = DateTime.Now.AddYears(10).ToString("yyyy-MM-dd HH:mm:ss");

        // Try P/Invoke first (proven to program cards correctly)
        var ioFlag = new IntPtr(3);
        var result = CareaIfcNative.addUnSortCard(device.SerialNumber, device.IP, device.TCPPort, device.Password,
            1, cardNo, cardPassword, openMode, ioFlag, effectiveTimes, openLock, permitTime,
            timePeriodIndex.ToString(), holidayEnabled ? 1 : 0);
        SdkLog($"addUnSortCard() returned: {result} for device {device.SerialNumber}, card {cardNo}, effectiveTimes={effectiveTimes}, permitTime={permitTime}, openLock={openLock}");

        if (result >= 0)
            return; // Success via P/Invoke

        // P/Invoke failed (TCP stuck after monitoring) — skip ConnectMain (returns True but doesn't work)
        // Go directly to subprocess (fresh process = clean native DLL state)
        SdkLog($"Trying subprocess fallback for card {cardNo}...");
        try
        {
            var subprocessResult = AddCardViaSubprocessSync(device, cardNo, cardPassword, openMode,
                openLock, permitTime, effectiveTimes, timePeriodIndex.ToString(), holidayEnabled ? 1 : 0);
            if (subprocessResult)
            {
                SdkLog($"Subprocess fallback succeeded for card {cardNo}");
                return;
            }
            SdkLog($"Subprocess fallback returned false for card {cardNo}");
        }
        catch (Exception subEx)
        {
            SdkLog($"Subprocess fallback exception: {subEx.Message}");
        }

        throw new InvalidOperationException($"Failed to add card {cardNo} to device {device.SerialNumber} ({device.IP}). P/Invoke, ConnectMain, and subprocess all failed.");
    }

    /// <summary>
    /// Spawns a separate process to execute addUnSortCard with fresh native DLL state.
    /// This bypasses the corrupted TCP state caused by monitoring.
    /// </summary>
    private bool AddCardViaSubprocessSync(DeviceInfo device, string cardNo, string cardPassword,
        int openMode, string doorPermissions, string permitTime,
        int effectiveTimes, string timePeriodIndex, int holidayEnabled)
    {
        var paramsFile = Path.Combine(Path.GetTempPath(), $"cardsync_{Guid.NewGuid():N}.tmp");
        var cardSyncExe = Path.Combine(AppContext.BaseDirectory, "AccessControlPro.CardSync.exe");

        // Fallback: if CardSync.exe not found, look for CardSync.dll
        if (!File.Exists(cardSyncExe))
        {
            var cardSyncDll = Path.Combine(AppContext.BaseDirectory, "AccessControlPro.CardSync.dll");
            if (File.Exists(cardSyncDll))
                cardSyncExe = cardSyncDll;
            else
            {
                SdkLog("AddCardViaSubprocess: CardSync.exe not found");
                return false;
            }
        }

        try
        {
            // Write params file
            var lines = new[]
            {
                device.SerialNumber,
                device.IP,
                device.TCPPort.ToString(),
                device.Password ?? "",
                cardNo,
                cardPassword ?? "",
                openMode.ToString(),
                doorPermissions,
                permitTime,
                effectiveTimes.ToString(),
                timePeriodIndex ?? "00000000",
                holidayEnabled.ToString()
            };
            File.WriteAllLines(paramsFile, lines);

            SdkLog($"AddCardViaSubprocess: Spawning for card {cardNo} on device {device.SerialNumber}");

            var psi = new ProcessStartInfo
            {
                FileName = cardSyncExe.EndsWith(".dll") ? "dotnet" : cardSyncExe,
                Arguments = cardSyncExe.EndsWith(".dll") ? $"\"{cardSyncExe}\" \"{paramsFile}\"" : $"\"{paramsFile}\"",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                SdkLog("AddCardViaSubprocess: Failed to start process");
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            var exited = process.WaitForExit(30000);
            if (!exited)
            {
                process.Kill();
                SdkLog("AddCardViaSubprocess: Process timed out (30s)");
                return false;
            }

            var result = output.Trim();
            var success = process.ExitCode == 0 && result == "1";

            SdkLog($"AddCardViaSubprocess: ExitCode={process.ExitCode}, Output={result}, Error={error.Trim()}, Success={success}");
            return success;
        }
        catch (Exception ex)
        {
            SdkLog($"AddCardViaSubprocess: Exception - {ex.Message}");
            return false;
        }
        finally
        {
            try { if (File.Exists(paramsFile)) File.Delete(paramsFile); } catch { }
        }
    }

    /// <summary>
    /// Write (add/update) a card using FC8900Command.WriteCardList via ConnectMain.
    /// Uses the same TCP connection as monitoring — avoids TCP conflict/stuck issue
    /// that occurs when addUnSortCard() creates a competing TCP connection.
    /// </summary>
    public void WriteCardViaConnectMain(DeviceInfo device, string cardNo, string cardPassword,
        int openMode, string openLock, string permitTime, int effectiveTimes = 1,
        int timePeriodIndex = 0, bool holidayEnabled = false)
    {
        Initialize();
        if (_connectMain == null)
            throw new InvalidOperationException("SDK not initialized — ConnectMain is null");

        var info = BuildConnectInfo(device);

        // Parse and sanitize expiry date
        if (!DateTime.TryParse(permitTime, out var expiryDate) || expiryDate.Year < 2024)
            expiryDate = DateTime.Now.AddYears(10);

        var card = new FCardCDrive.FC8900.FC8900Card();
        card.Card = uint.Parse(cardNo);
        card.Password = cardPassword ?? "";
        card.ExpiryDate = expiryDate;
        card.OpenCount = (ushort)(effectiveTimes < 0 ? 0 : effectiveTimes);
        card.CardType = FCardCDrive.Card.eCardType.eNormal;
        card.StandardCard = true;

        // Parse door permissions from openLock string "01010000" → door 0,1 enabled
        bool[] doorPerms = new bool[4];
        if (openLock != null && openLock.Length >= 8)
        {
            doorPerms[0] = openLock.Substring(0, 2) == "01";
            doorPerms[1] = openLock.Substring(2, 2) == "01";
            doorPerms[2] = openLock.Substring(4, 2) == "01";
            doorPerms[3] = openLock.Substring(6, 2) == "01";
        }
        else
        {
            doorPerms[0] = doorPerms[1] = doorPerms[2] = doorPerms[3] = true;
        }

        // Set indexed properties via reflection (VB.NET parameterized properties)
        var cardType = card.GetType();
        var setDoor = cardType.GetMethod("set_Door");
        var setTimeGroup = cardType.GetMethod("set_TimeGroupNum");
        var setHoliday = cardType.GetMethod("set_Holiday");
        byte tg = (byte)(timePeriodIndex > 0 ? timePeriodIndex : 1);

        for (byte doorIdx = 0; doorIdx < 4; doorIdx++)
        {
            setDoor?.Invoke(card, new object[] { doorIdx, doorPerms[doorIdx] });
            setTimeGroup?.Invoke(card, new object[] { doorIdx, tg });
            setHoliday?.Invoke(card, new object[] { doorIdx, holidayEnabled });
        }

        SdkLog($"WriteSortCardList card: num={card.Card}, expiry={card.ExpiryDate}, openCount={card.OpenCount}, standard={card.StandardCard}, doors=[{doorPerms[0]},{doorPerms[1]},{doorPerms[2]},{doorPerms[3]}], setDoor={setDoor != null}, setTG={setTimeGroup != null}");

        var cardList = new System.Collections.Generic.List<FCardCDrive.Card> { card };

        // Try WriteSortCardList first (indexed card area — needed for card to work at reader)
        var result = _connectMain.Command(info, "WriteSortCardList", cardList);
        SdkLog($"WriteSortCardList via ConnectMain returned: {result} for device {device.SerialNumber}, card {cardNo}");

        if (!result)
        {
            // Fallback to WriteCardList (non-indexed area)
            result = _connectMain.Command(info, "WriteCardList", cardList);
            SdkLog($"WriteCardList fallback returned: {result} for device {device.SerialNumber}, card {cardNo}");
        }

        if (!result)
            throw new InvalidOperationException($"Both WriteSortCardList and WriteCardList failed for card {cardNo} on device {device.SerialNumber}");
    }

    /// <summary>
    /// Delete a card from device using FC8900Command.DeleteCardList via ConnectMain.
    /// Uses the same TCP connection as monitoring — avoids TCP conflict with addUnSortCard.
    /// </summary>
    public void DeleteCardViaConnectMain(DeviceInfo device, string cardNo)
    {
        Initialize();
        if (_connectMain == null)
            throw new InvalidOperationException("SDK not initialized — ConnectMain is null");

        var info = BuildConnectInfo(device);

        var card = new FCardCDrive.FC8900.FC8900Card
        {
            Card = uint.Parse(cardNo)
        };

        var cardList = new System.Collections.Generic.List<FCardCDrive.Card> { card };
        var result = _connectMain.Command(info, "DeleteCardList", cardList);
        SdkLog($"DeleteCardList via ConnectMain returned: {result} for device {device.SerialNumber}, card {cardNo}");
        if (!result)
            SdkLog($"WARNING: DeleteCardList returned false for card {cardNo} on device {device.SerialNumber} — card may not have existed");
    }

    public void SetDoorPassword(DeviceInfo device, string password)
    {
        var s = BuildDeviceParams(device);
        var result = CareaIfcNative.setOpenDoorPwd(s, password);
        SdkLog($"setOpenDoorPwd() returned: {result} for device {device.SerialNumber}");
    }

    public string GetDeviceInfo(DeviceInfo device)
    {
        var ptr = CareaIfcNative.getDevInfo(device.SerialNumber, device.IP, device.TCPPort, device.Password, "");
        return ptr != IntPtr.Zero ? Marshal.PtrToStringUni(ptr) ?? string.Empty : string.Empty;
    }

    public int GetRecords(DeviceInfo device, int recordType)
    {
        return CareaIfcNative.getRecord(device.SerialNumber, device.IP, device.TCPPort, device.Password, recordType);
    }

    public void SetAntiPassback(DeviceInfo device, int mode, string doorSelect)
    {
        var s = BuildDeviceParams(device);
        var result = CareaIfcNative.setAntiSneakBack(mode, s, doorSelect);
        SdkLog($"setAntiSneakBack() returned: {result} for device {device.SerialNumber}, mode {mode}");
    }

    public void TriggerAlarm(DeviceInfo device, int alarmAction)
    {
        var s = BuildDeviceParams(device);
        var result = CareaIfcNative.policeOfficer(s, alarmAction);
        SdkLog($"policeOfficer() returned: {result} for device {device.SerialNumber}, action {alarmAction}");
    }

    public string GetFireAlarmStatus(DeviceInfo device)
    {
        var s = BuildDeviceParams(device);
        return CareaIfcNative.fireAlarm(s, "") ?? string.Empty;
    }

    public void StartMonitoring(List<DeviceInfo> devices, Action<MonitorEvent> onEvent)
    {
        StopMonitoring();
        Initialize();

        lock (_monitorLock)
        {
            _monitoredDevices = devices;
            _onMonitorEvent = onEvent;
        }
        _monitorMain = new ConnectMain();
        _monitorMain.WatchEvent += OnWatchEvent;
        _monitorMain.CommandAchieve += (_, _, _) => { };
        _monitorMain.CommandTimeout += (_, _, _, _) => { };
        _monitorMain.ConnectError += (_, _, _) => { };
        _monitorMain.PasswordError += (_, _, _) => { };

        foreach (var device in devices)
            SendBeginWatch(device);

        // Keepalive timer — re-send BeginWatch every 10 seconds to prevent device disconnect
        _keepAliveTimer = new System.Timers.Timer(10000);
        _keepAliveTimer.Elapsed += (_, _) =>
        {
            List<DeviceInfo>? devices;
            lock (_monitorLock)
            {
                devices = _monitoredDevices;
            }
            if (devices == null) return;
            foreach (var device in devices)
                try { SendBeginWatch(device); } catch { }
        };
        _keepAliveTimer.AutoReset = true;
        _keepAliveTimer.Enabled = true;

        SdkLog($"StartMonitoring: watching {devices.Count} device(s)");
    }

    public void StopMonitoring()
    {
        _keepAliveTimer?.Dispose();
        _keepAliveTimer = null;

        List<DeviceInfo>? devicesToClose;
        ConnectMain? monitorToClose;
        lock (_monitorLock)
        {
            devicesToClose = _monitoredDevices;
            monitorToClose = _monitorMain;
        }

        // Send CloseWatch BEFORE nulling _monitorMain (so SendCloseWatch can use it)
        if (monitorToClose != null && devicesToClose != null)
        {
            foreach (var device in devicesToClose)
            {
                try
                {
                    var info = new ConnectInfo
                    {
                        SN = device.SerialNumber,
                        IP = device.IP,
                        NetPort = (ushort)device.TCPPort,
                        Password = device.Password,
                        EquptType = GetEquipmentType(device.SerialNumber),
                        ConnType = ConnectInfo.e_ConnectType.OnTCPClient,
                        RestartCount = 1,
                        TimeOutMSEL = 3000
                    };
                    monitorToClose.Command(info, "CloseWatch");
                }
                catch { }
            }
            // Wait for device to process CloseWatch and release TCP
            Thread.Sleep(2000);
        }

        // NOW null everything
        lock (_monitorLock)
        {
            _monitorMain = null;
            _monitoredDevices = null;
            _onMonitorEvent = null;
        }
        SdkLog("StopMonitoring: stopped");
    }

    /// <summary>
    /// Temporarily pause monitoring (close watch on all devices).
    /// SDK can't handle card operations while monitoring is active.
    /// </summary>
    public void PauseMonitoring()
    {
        _keepAliveTimer?.Stop();
        List<DeviceInfo>? devices;
        lock (_monitorLock)
        {
            devices = _monitoredDevices;
        }
        if (devices != null)
        {
            foreach (var device in devices)
                try { SendCloseWatch(device); } catch { }
        }
        SdkLog("PauseMonitoring: paused");
    }

    /// <summary>
    /// Resume monitoring after pause (re-send BeginWatch to all devices).
    /// </summary>
    public void ResumeMonitoring()
    {
        List<DeviceInfo>? devices;
        lock (_monitorLock)
        {
            devices = _monitoredDevices;
        }
        if (devices != null)
        {
            foreach (var device in devices)
                try { SendBeginWatch(device); } catch { }
        }
        _keepAliveTimer?.Start();
        SdkLog("ResumeMonitoring: resumed");
    }

    private void SendBeginWatch(DeviceInfo device)
    {
        var info = new ConnectInfo
        {
            SN = device.SerialNumber,
            IP = device.IP,
            NetPort = (ushort)device.TCPPort,
            Password = device.Password,
            EquptType = GetEquipmentType(device.SerialNumber),
            ConnType = ConnectInfo.e_ConnectType.OnTCPClient,
            RestartCount = 3,
            TimeOutMSEL = 600
        };
        _monitorMain?.Command(info, "BeginWatch");
    }

    private void SendCloseWatch(DeviceInfo device)
    {
        var info = new ConnectInfo
        {
            SN = device.SerialNumber,
            IP = device.IP,
            NetPort = (ushort)device.TCPPort,
            Password = device.Password,
            EquptType = GetEquipmentType(device.SerialNumber),
            ConnType = ConnectInfo.e_ConnectType.OnTCPClient,
            RestartCount = 3,
            TimeOutMSEL = 600
        };
        _monitorMain?.Command(info, "CloseWatch");
    }

    // Win32 imports for auto-closing native SDK dialogs
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private const uint WM_CLOSE = 0x0010;
    private const uint BM_CLICK = 0x00F5;

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    // Win32 message pump for STA thread — native DLL needs this to process dialogs
    [DllImport("user32.dll")]
    private static extern int GetMessage(out NativeMsg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref NativeMsg lpMsg);
    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref NativeMsg lpMsg);
    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out NativeMsg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMsg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }
    private const uint PM_REMOVE = 0x0001;
    private const uint WM_USER = 0x0400;

    /// <summary>
    /// Find and auto-close ANY native SDK dialog (Export, No new records, etc.)
    /// by detecting dialogs from our process that have an OK button.
    /// IMPORTANT: Do NOT hide dialogs before clicking — native DLL may check visibility.
    /// </summary>
    private static void AutoCloseExportDialog()
    {
        try
        {
            var currentPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows((hWnd, _) =>
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != currentPid) return true;
                if (!IsWindowVisible(hWnd)) return true;

                // Check if this is a standard dialog (#32770) or has SDK-related content
                var classSb = new System.Text.StringBuilder(256);
                GetClassName(hWnd, classSb, 256);
                var className = classSb.ToString();

                var titleSb = new System.Text.StringBuilder(512);
                GetWindowText(hWnd, titleSb, 512);
                var title = titleSb.ToString();

                // Match: standard dialog class, OR title contains app path / Export / record keywords
                bool isNativeDialog = className == "#32770" ||
                    title.Contains("Export", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("record", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("AccessControlPro", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains(".exe", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("net8.0", StringComparison.OrdinalIgnoreCase);

                if (isNativeDialog)
                {
                    // Find and click OK button to dismiss properly
                    // Do NOT hide the dialog first — native DLL may need it visible
                    var okBtn = FindChildByText(hWnd, "OK");
                    if (okBtn != IntPtr.Zero)
                    {
                        SendMessage(okBtn, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                        return true;
                    }
                    // Also try "Yes" buttons
                    var yesBtn = FindChildByText(hWnd, "Yes");
                    if (yesBtn != IntPtr.Zero)
                    {
                        SendMessage(yesBtn, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                        return true;
                    }
                    // Fallback — close the window
                    SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);
        }
        catch { }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    private static IntPtr FindChildByText(IntPtr parentHwnd, string text)
    {
        const uint GW_CHILD = 5;
        const uint GW_HWNDNEXT = 2;
        var child = GetWindow(parentHwnd, GW_CHILD);
        while (child != IntPtr.Zero)
        {
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(child, sb, 256);
            if (sb.ToString().Contains(text, StringComparison.OrdinalIgnoreCase))
                return child;
            child = GetWindow(child, GW_HWNDNEXT);
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Run getRecord on an STA thread with a Win32 message pump.
    /// The native DLL (CareaIfc) expects a proper message loop to handle
    /// its internal dialogs (progress, export complete, etc.) — without one,
    /// the CSV may not be written reliably. This mirrors the demo which runs
    /// getRecord on a WinForms UI thread (ShowDialog → full message pump).
    /// </summary>
    private int RunGetRecordWithMessagePump(DeviceInfo device, int recordType, int timeoutSeconds)
    {
        int result = 0;
        uint staThreadId = 0;
        var threadReady = new ManualResetEventSlim(false);
        var getRecordDone = new ManualResetEventSlim(false);

        var staThread = new Thread(() =>
        {
            staThreadId = GetCurrentThreadId();
            threadReady.Set();

            // Call getRecord — this may show native dialogs that need the message pump
            try
            {
                result = CareaIfcNative.getRecord(
                    device.SerialNumber, device.IP, device.TCPPort, device.Password, recordType);
            }
            catch (Exception ex)
            {
                SdkLog($"getRecord type={recordType} error: {ex.Message}");
            }
            finally
            {
                getRecordDone.Set();
            }

            // Pump any remaining messages after getRecord returns
            // This ensures file writes and cleanup operations complete
            NativeMsg msg;
            while (PeekMessage(out msg, IntPtr.Zero, 0, 0, PM_REMOVE))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        });
        staThread.SetApartmentState(ApartmentState.STA);
        staThread.IsBackground = true;
        staThread.Start();

        // Wait for the STA thread to finish (max timeout per record type)
        if (!staThread.Join(TimeSpan.FromSeconds(timeoutSeconds)))
        {
            SdkLog($"getRecord type={recordType} timed out after {timeoutSeconds}s");
            // Force-close any lingering dialogs
            AutoCloseExportDialog();
            return 0;
        }

        return result;
    }

    /// <summary>
    /// Read CSV with retries — the native DLL may not have flushed the file when getRecord returns.
    /// </summary>
    private List<MonitorEvent> ReadCsvWithRetry(string csvPath, string deviceSN, string deviceIP, int recordType, int maxRetries = 3)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (!File.Exists(csvPath))
                {
                    SdkLog($"CSV does not exist (attempt {attempt + 1})");
                    Thread.Sleep(500);
                    continue;
                }

                var fileInfo = new FileInfo(csvPath);
                SdkLog($"CSV file size={fileInfo.Length} bytes (attempt {attempt + 1})");

                // If file is too small (just header ~52 bytes), wait and retry
                if (fileInfo.Length < 60 && attempt < maxRetries - 1)
                {
                    SdkLog($"CSV appears empty (only header), waiting...");
                    Thread.Sleep(800);
                    continue;
                }

                var parsed = ParseRecordListCsv(csvPath, deviceSN, deviceIP, recordType);
                if (parsed.Count > 0 || attempt == maxRetries - 1)
                    return parsed;

                SdkLog($"Parsed 0 records, retrying...");
                Thread.Sleep(500);
            }
            catch (IOException ex)
            {
                SdkLog($"CSV read error (attempt {attempt + 1}): {ex.Message}");
                Thread.Sleep(500);
            }
        }
        return new List<MonitorEvent>();
    }

    public async Task<List<MonitorEvent>> FetchAllRecordsAsync(DeviceInfo device, int timeoutSeconds = 30)
    {
        Initialize();
        var allRecords = new List<MonitorEvent>();
        var csvPath = Path.Combine(AppContext.BaseDirectory, "RecordList.csv");

        SdkLog($"FetchAllRecords: START for {device.SerialNumber}, CWD={Environment.CurrentDirectory}, BaseDir={AppContext.BaseDirectory}");

        // Warm up the TCP connection before downloading records
        try
        {
            SdkLog("FetchAllRecords: warming up connection with getDevInfo...");
            var info = GetDeviceInfo(device);
            SdkLog($"FetchAllRecords: warmup result length={info?.Length ?? 0}");
            await Task.Delay(1000); // Give device time to be ready
        }
        catch (Exception ex)
        {
            SdkLog($"FetchAllRecords: warmup failed (continuing anyway): {ex.Message}");
        }

        // Record types: Card=1, Button=0, DoorSensor=2, Software=3, System=4, Alarm=5
        var recordTypes = new[] { 1, 0, 2, 3, 4, 5 };

        foreach (var rt in recordTypes)
        {
            try
            {
                var records = await FetchRecordTypeAsync(device, rt, csvPath, timeoutSeconds);

                // If Card records (type 1) returned 0 records, retry once after a delay
                // This is the primary record type and the native DLL sometimes needs a second attempt
                if (rt == 1 && records.Count == 0)
                {
                    SdkLog("FetchAllRecords: Card records returned 0, retrying after 3s...");
                    await Task.Delay(3000);
                    records = await FetchRecordTypeAsync(device, rt, csvPath, timeoutSeconds);
                    SdkLog($"FetchAllRecords: Card records retry got {records.Count} records");
                }

                allRecords.AddRange(records);

                // Delay between record type calls to let native DLL clean up
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                SdkLog($"FetchAllRecords type={rt} error: {ex.Message}");
            }
        }

        SdkLog($"FetchAllRecords: device={device.SerialNumber} total={allRecords.Count} records");
        return allRecords;
    }

    private async Task<List<MonitorEvent>> FetchRecordTypeAsync(DeviceInfo device, int rt, string csvPath, int timeoutSeconds)
    {
        // Delete old CSV before each call so we only get fresh data
        try
        {
            if (File.Exists(csvPath))
                File.Delete(csvPath);
        }
        catch (IOException ex)
        {
            SdkLog($"Could not delete CSV: {ex.Message}");
            await Task.Delay(500);
            try { if (File.Exists(csvPath)) File.Delete(csvPath); } catch { }
        }

        SdkLog($"FetchRecordType: calling getRecord type={rt} for {device.SerialNumber}");

        // Start auto-closer that polls for native dialogs immediately.
        // Clicks OK/Yes on any native SDK dialogs (export complete, no records, etc.)
        var staFinished = false;
        var dialogCloser = Task.Run(async () =>
        {
            for (int i = 0; i < 600 && !staFinished; i++)
            {
                AutoCloseExportDialog();
                await Task.Delay(100);
            }
        });

        // Run getRecord on STA thread with message pump
        int result = RunGetRecordWithMessagePump(device, rt, timeoutSeconds);
        staFinished = true;

        SdkLog($"getRecord type={rt} returned {result}");

        // Close any remaining dialogs
        AutoCloseExportDialog();

        // Wait for native DLL to finish writing CSV — critical for reliability
        await Task.Delay(1500);
        AutoCloseExportDialog();

        // Parse CSV with retry logic
        if (result == 1)
        {
            var parsed = ReadCsvWithRetry(csvPath, device.SerialNumber, device.IP, rt);
            SdkLog($"Parsed {parsed.Count} records from CSV for type={rt}");
            return parsed;
        }

        SdkLog($"getRecord type={rt} returned {result} (not success)");
        return new List<MonitorEvent>();
    }

    /// <summary>
    /// Parse RecordList.csv exported by native SDK.
    /// Format: Numbering,CardNumber,DoorNumber,In^out,status,Time
    /// </summary>
    private static List<MonitorEvent> ParseRecordListCsv(string csvPath, string deviceSN, string deviceIP, int recordType)
    {
        var records = new List<MonitorEvent>();
        try
        {
            var lines = File.ReadAllLines(csvPath);
            // Skip header line
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = line.Split(',');
                if (parts.Length < 6) continue;

                // Numbering,CardNumber,DoorNumber,In^out,status,Time
                var cardNumber = parts[1].Trim();
                int.TryParse(parts[2].Trim(), out int doorNumber);
                int.TryParse(parts[3].Trim(), out int readerType); // In^out: 0=in, 1=out
                int.TryParse(parts[4].Trim(), out int eventCode);

                DateTime eventDate = DateTime.UtcNow;
                if (parts.Length >= 6)
                {
                    var timeStr = parts[5].Trim();
                    // Handle case where time field may contain commas (date format: yyyy/MM/dd HH:mm:ss)
                    if (parts.Length > 6)
                        timeStr = string.Join(",", parts.Skip(5)).Trim();
                    if (DateTime.TryParse(timeStr, out var parsedDate))
                        eventDate = parsedDate.Kind == DateTimeKind.Unspecified ?
                            DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc) : parsedDate;
                }

                records.Add(new MonitorEvent(
                    DeviceSN: deviceSN,
                    DeviceIP: deviceIP,
                    RecordType: SdkRecordTypeToDomain(recordType),
                    EventCode: eventCode,
                    EventDate: eventDate,
                    CardNumber: cardNumber == "0" ? "" : cardNumber,
                    DoorNumber: doorNumber,
                    ReaderType: readerType
                ));
            }
        }
        catch (Exception ex)
        {
            SdkLog($"ParseRecordListCsv error: {ex.Message}");
        }
        return records;
    }

    /// <summary>
    /// Map SDK getRecord type index to Domain RecordType enum.
    /// SDK uses: Card=1, Button=0  but Domain enum uses: Card=0, Button=1
    /// Types 2-5 are the same in both.
    /// </summary>
    private static int SdkRecordTypeToDomain(int sdkType) => sdkType switch
    {
        1 => 0,  // SDK Card(1) → Domain Card(0)
        0 => 1,  // SDK Button(0) → Domain Button(1)
        _ => sdkType  // 2=DoorSensor, 3=Software, 4=Alarm, 5=System — same
    };

    private static ConnectInfo.e_EquptType GetEquipmentType(string sn)
    {
        if (sn.Contains("CR-3212T") || sn.Contains("CR-3222T") || sn.Contains("CR-3242T") ||
            sn.Contains("CR-3216H") || sn.Contains("CR-3226H") || sn.Contains("CR-3246H"))
            return ConnectInfo.e_EquptType.FC8900H;
        return ConnectInfo.e_EquptType.FC8900;
    }

    private void OnWatchEvent(ConnectInfo oInfo, int iRecordCode, byte[] bData)
    {
        try
        {
            // Skip control messages (heartbeat, connection confirm, online/offline)
            if (iRecordCode is 0x22 or 0x23 or 0x24 or 0x25 or 0xF0)
                return;

            ConnectMain? monitorMain;
            Action<MonitorEvent>? onEvent;
            lock (_monitorLock)
            {
                monitorMain = _monitorMain;
                onEvent = _onMonitorEvent;
            }
            if (monitorMain == null) return;

            Record? oRecord = null;
            var equipType = GetEquipmentType(oInfo.SN);
            if (!monitorMain.WatchRecordDecompile(equipType, iRecordCode, bData, ref oRecord))
                return;
            if (oRecord == null) return;

            var evt = new MonitorEvent(
                DeviceSN: oInfo.SN,
                DeviceIP: oInfo.IP,
                RecordType: (int)oRecord.RecordType,
                EventCode: (int)oRecord.EventCode,
                EventDate: oRecord.EventDate == DateTime.MinValue ? DateTime.UtcNow : oRecord.EventDate,
                CardNumber: oRecord.Card > 0 ? oRecord.Card.ToString() : "",
                DoorNumber: oRecord.DoorNum,
                ReaderType: oRecord.ReaderType
            );

            SdkLog($"WatchEvent: SN={evt.DeviceSN} Type={evt.RecordType} Code={evt.EventCode} Card={evt.CardNumber} Door={evt.DoorNumber}");
            onEvent?.Invoke(evt);
        }
        catch (Exception ex)
        {
            SdkLog($"WatchEvent ERROR: {ex.Message}");
        }
    }
}
