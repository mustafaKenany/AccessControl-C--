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
    private static readonly string SdkLogPath = Path.Combine(AppContext.BaseDirectory, "sdk_log.txt");

    private static void SdkLog(string msg)
    {
        try { File.AppendAllText(SdkLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n"); }
        catch { /* ignore */ }
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

    public void Shutdown()
    {
        if (_initialized)
        {
            CareaIfcNative.clearNet();
            _connectMain = null;
            _initialized = false;
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
            CareaIfcNative.install(true, s, doorCount);
            File.AppendAllText(logPath, "install() returned successfully\n");
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
        CareaIfcNative.calibrationTime(s);
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

        CareaIfcNative.updateIP(s);
    }

    public void RemoteOpenDoor(DeviceInfo device, int[] doorNumbers)
    {
        if (_connectMain == null) return;
        var info = BuildConnectInfo(device);
        var doors = new bool[4];
        foreach (var d in doorNumbers)
            if (d >= 0 && d < 4) doors[d] = true;
        _connectMain.Command(info, "OpenRelay", doors);
    }

    public void RemoteCloseDoor(DeviceInfo device, int[] doorNumbers)
    {
        if (_connectMain == null) return;
        var info = BuildConnectInfo(device);
        var doors = new bool[4];
        foreach (var d in doorNumbers)
            if (d >= 0 && d < 4) doors[d] = true;
        _connectMain.Command(info, "CloseRelay", doors);
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
        CareaIfcNative.openTimeDelay(s, 3, doorSelect, delaySeconds.ToString().PadLeft(4, '0'));
    }

    public void SetOpeningHours(DeviceInfo device, int timeNum, string timePieces)
    {
        var s = BuildDeviceParams(device);
        CareaIfcNative.setTimes(s, 1, timeNum, timePieces);
    }

    public void AddAccessCard(DeviceInfo device, string cardNo, string cardPassword, int openMode, string openLock, string permitTime, int effectiveTimes = 1, int timePeriodIndex = 0, bool holidayEnabled = false)
    {
        var ioFlag = new IntPtr(3); // Must be 3 per demo CDlgGrant — enables door IO
        CareaIfcNative.addUnSortCard(device.SerialNumber, device.IP, device.TCPPort, device.Password,
            1, cardNo, cardPassword, openMode, ioFlag, effectiveTimes, openLock, permitTime,
            timePeriodIndex.ToString(), holidayEnabled ? 1 : 0);
    }

    public void SetDoorPassword(DeviceInfo device, string password)
    {
        var s = BuildDeviceParams(device);
        CareaIfcNative.setOpenDoorPwd(s, password);
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
        CareaIfcNative.setAntiSneakBack(mode, s, doorSelect);
    }

    public void TriggerAlarm(DeviceInfo device, int alarmAction)
    {
        var s = BuildDeviceParams(device);
        CareaIfcNative.policeOfficer(s, alarmAction);
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
            _monitorMain = null;
            _monitoredDevices = null;
            _onMonitorEvent = null;
        }

        if (monitorToClose != null && devicesToClose != null)
        {
            foreach (var device in devicesToClose)
                try { SendCloseWatch(device); } catch { }
        }
        SdkLog("StopMonitoring: stopped");
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

        // Start auto-closer that polls for native dialogs.
        // It starts after 2 seconds (to let the progress dialog do its work)
        // and then clicks OK/Yes on any completion dialogs.
        var staFinished = false;
        var dialogCloser = Task.Run(async () =>
        {
            await Task.Delay(2000);
            for (int i = 0; i < 150 && !staFinished; i++)
            {
                AutoCloseExportDialog();
                await Task.Delay(200);
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
