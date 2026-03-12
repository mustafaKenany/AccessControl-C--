using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Native;

namespace AccessControlPro.SDK.Wrapper;

public class AccessControlSdkWrapper : IAccessControlSdk
{
    private bool _initialized;

    private static string[] BuildDeviceParams(DeviceInfo device)
    {
        var s = new string[24];
        s[0] = "s";
        s[1] = device.IP;
        s[2] = device.TCPPort.ToString();
        s[3] = device.SerialNumber;
        s[4] = device.Password;
        s[16] = device.MAC;
        s[17] = device.SubnetMask;
        s[18] = device.Gateway;
        s[19] = "0.0.0.0";
        s[20] = "0.0.0.0";
        s[21] = "server";
        s[22] = device.TCPPort.ToString();
        s[23] = device.UDPPort.ToString();
        return s;
    }

    public void Initialize()
    {
        if (!_initialized)
        {
            CareaIfcNative.PreloadNativeLibraries();
            CareaIfcNative.initNet("AccessControlPro");
            _initialized = true;
        }
    }

    public void Shutdown()
    {
        if (_initialized)
        {
            CareaIfcNative.clearNet();
            _initialized = false;
        }
    }

    public DeviceInfo? SearchDevice()
    {
        var info = CareaIfcNative.devInfo2();
        if (string.IsNullOrEmpty(info)) return null;

        var parts = info.Split(',');
        if (parts.Length < 5) return null;

        return new DeviceInfo
        {
            IP = parts[0],
            MAC = parts[1],
            SerialNumber = parts[2],
            TCPPort = int.TryParse(parts[3], out var tcp) ? tcp : 8000,
            UDPPort = int.TryParse(parts[4], out var udp) ? udp : 8101
        };
    }

    public void InitializeDevice(DeviceInfo device, int doorCount)
    {
        var s = BuildDeviceParams(device);
        CareaIfcNative.install(true, s, doorCount);
    }

    public string ReadDeviceTime(DeviceInfo device)
    {
        var s = BuildDeviceParams(device);
        return CareaIfcNative.readDevNowTime(s, "") ?? string.Empty;
    }

    public void CalibrateTime(DeviceInfo device)
    {
        var s = BuildDeviceParams(device);
        CareaIfcNative.calibrationTime(s);
    }

    public void UpdateIP(DeviceInfo device)
    {
        var s = BuildDeviceParams(device);
        CareaIfcNative.updateIP(s);
    }

    public void RemoteOpenDoor(DeviceInfo device, int[] doorNumbers)
    {
        CareaIfcNative.remoteOpen(device.SerialNumber, device.IP, device.TCPPort, device.Password, doorNumbers, doorNumbers.Length, 0);
    }

    public void RemoteCloseDoor(DeviceInfo device, int[] doorNumbers)
    {
        CareaIfcNative.remoteOpen(device.SerialNumber, device.IP, device.TCPPort, device.Password, doorNumbers, doorNumbers.Length, 1);
    }

    public void SetDoorDelay(DeviceInfo device, int delaySeconds)
    {
        var s = BuildDeviceParams(device);
        CareaIfcNative.openTimeDelay(s, 1, Array.Empty<string>(), delaySeconds.ToString());
    }

    public void SetOpeningHours(DeviceInfo device, int timeNum, string timePieces)
    {
        var s = BuildDeviceParams(device);
        CareaIfcNative.setTimes(s, 1, timeNum, timePieces);
    }

    public void AddAccessCard(DeviceInfo device, string cardNo, string cardPassword, int openMode, string openLock, string permitTime)
    {
        CareaIfcNative.addUnSortCard(device.SerialNumber, device.IP, device.TCPPort, device.Password,
            1, cardNo, cardPassword, openMode, IntPtr.Zero, 1, openLock, permitTime, "0", 0);
    }

    public void SetDoorPassword(DeviceInfo device, string password)
    {
        var s = BuildDeviceParams(device);
        CareaIfcNative.setOpenDoorPwd(s, password);
    }

    public string GetDeviceInfo(DeviceInfo device)
    {
        return CareaIfcNative.getDevInfo(device.SerialNumber, device.IP, device.TCPPort, device.Password, "") ?? string.Empty;
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
