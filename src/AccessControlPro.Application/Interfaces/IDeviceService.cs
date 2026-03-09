using AccessControlPro.Application.DTOs;

namespace AccessControlPro.Application.Interfaces;

public interface IDeviceService
{
    Task<IEnumerable<DeviceDto>> GetAllDevicesAsync();
    Task<DeviceDto?> GetDeviceByIdAsync(int id);
    Task AddDeviceAsync(DeviceDto device);
    Task DeleteDeviceAsync(int id);
    Task<DeviceDto?> SearchNetworkAsync();
    Task<bool> ConnectDeviceAsync(int deviceId);
    Task<string> GetDeviceInfoAsync(int deviceId);
    Task<bool> RemoteOpenDoorAsync(int deviceId, int doorNumber);
    Task<bool> RemoteOpenAllDoorsAsync(int deviceId);
    Task<bool> SyncTimeAsync(int deviceId);
    Task<bool> RenameDeviceAsync(int deviceId, string newName);
    Task<bool> ChangeIPAsync(int deviceId, string newIP, string subnet, string gateway);
}
