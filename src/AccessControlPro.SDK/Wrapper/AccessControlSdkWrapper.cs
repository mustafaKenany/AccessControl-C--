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

    public async Task<DeviceInfo?> SearchDeviceAsync(int timeoutMs = 15000)
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
                    var retryInfo = new ConnectInfo
                    {
                        ConnType = ConnectInfo.e_ConnectType.OnUDP,
                        IP = "",
                        NetPort = 8101,
                        UDPBroadcast = true,
                        EquptType = ConnectInfo.e_EquptType.FC8900,
                        SN = "MC-5824T25070244",
                        Password = "FFFFFFFF",
                        RestartCount = 0,
                        TimeOutMSEL = 5000
                    };
                    _connectMain!.SearchEquptOnNetNum(retryInfo, netNum, true);
                    SdkLog("Retry search command sent");
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
            var oInfo = new ConnectInfo
            {
                ConnType = ConnectInfo.e_ConnectType.OnUDP,
                IP = "",
                NetPort = 8101,
                UDPBroadcast = true,
                EquptType = ConnectInfo.e_EquptType.FC8900,
                SN = "MC-5824T25070244",
                Password = "FFFFFFFF",
                RestartCount = 0,
                TimeOutMSEL = 5000
            };

            SdkLog($"Calling SearchEquptOnNetNum: netNum={netNum}");
            var result = _connectMain.SearchEquptOnNetNum(oInfo, netNum, true);
            SdkLog($"SearchEquptOnNetNum returned: {result}");

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

        // Debug: log all values before native call
        var logPath = Path.Combine(AppContext.BaseDirectory, "install_log.txt");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== install called at {DateTime.Now} ===");
        sb.AppendLine($"doorCount={doorCount}");
        for (int i = 0; i < s.Length; i++)
            sb.AppendLine($"  s[{i}] = {(s[i] == null ? "NULL" : $"\"{s[i]}\"")}");
        File.WriteAllText(logPath, sb.ToString());

        CareaIfcNative.install(true, s, doorCount);
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
}
