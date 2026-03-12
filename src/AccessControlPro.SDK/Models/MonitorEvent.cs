namespace AccessControlPro.SDK.Models;

public record MonitorEvent(
    string DeviceSN,
    string DeviceIP,
    int RecordType,
    int EventCode,
    DateTime EventDate,
    string CardNumber,
    int DoorNumber,
    int ReaderType // 1 = Entry, other = Exit
);
