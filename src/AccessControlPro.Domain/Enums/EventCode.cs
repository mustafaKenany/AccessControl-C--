namespace AccessControlPro.Domain.Enums;

public enum EventCode
{
    CardOpen = 1,
    PasswordOpen = 2,
    CardAndPasswordOpen = 3,
    CardRepeat = 4,
    CardExpired = 5,
    CardInvalid = 6,
    ButtonOpen = 10,
    RemoteOpen = 20,
    RemoteClose = 21,
    DoorSensorOpen = 30,
    DoorSensorClose = 31,
    AlarmFire = 40,
    AlarmPolice = 41,
    AlarmGas = 42,
    AlarmMagnetic = 43,
    AlarmTheft = 44,
    AlarmAntiPassback = 45,
    SystemStartup = 50,
    SystemRestart = 51,
    SystemHighTemp = 52,
    SystemUPS = 53
}
