using AccessControlPro.SDK.Models;

namespace AccessControlPro.SDK.Wrapper;

public interface IAccessControlSdk
{
    void Initialize();
    void Shutdown();
    Task<DeviceInfo?> SearchDeviceAsync(int timeoutMs = 10000);
    void InitializeDevice(DeviceInfo device, int doorCount);
    string ReadDeviceTime(DeviceInfo device);
    void CalibrateTime(DeviceInfo device);
    void UpdateIP(DeviceInfo device, int doorCount);
    void RemoteOpenDoor(DeviceInfo device, int[] doorNumbers);
    void RemoteCloseDoor(DeviceInfo device, int[] doorNumbers);
    void SetDoorDelay(DeviceInfo device, int doorNumber, int delaySeconds);
    void SetOpeningHours(DeviceInfo device, int timeNum, string timePieces);
    void AddAccessCard(DeviceInfo device, string cardNo, string cardPassword, int openMode, string openLock, string permitTime, int effectiveTimes = 1, int timePeriodIndex = 0, bool holidayEnabled = false);
    void SetDoorPassword(DeviceInfo device, string password);
    string GetDeviceInfo(DeviceInfo device);
    int GetRecords(DeviceInfo device, int recordType);
    void SetAntiPassback(DeviceInfo device, int mode, string doorSelect);
    void TriggerAlarm(DeviceInfo device, int alarmAction);
    string GetFireAlarmStatus(DeviceInfo device);
}
