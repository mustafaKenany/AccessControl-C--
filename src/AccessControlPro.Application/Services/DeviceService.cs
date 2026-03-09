using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Wrapper;

namespace AccessControlPro.Application.Services;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IAccessControlSdk _sdk;

    public DeviceService(IDeviceRepository deviceRepository, IAccessControlSdk sdk)
    {
        _deviceRepository = deviceRepository;
        _sdk = sdk;
    }

    public async Task<IEnumerable<DeviceDto>> GetAllDevicesAsync()
    {
        var devices = await _deviceRepository.GetAllAsync();
        return devices.Select(d => new DeviceDto
        {
            Id = d.Id,
            Name = d.Name,
            IP = d.IP,
            MAC = d.MAC,
            SerialNumber = d.SerialNumber,
            DeviceType = d.DeviceType.ToString(),
            TCPPort = d.TCPPort,
            Gateway = d.Gateway,
            SubnetMask = d.SubnetMask,
            IsOnline = d.IsOnline,
            DoorCount = GetDoorCount(d.DeviceType.ToString())
        });
    }

    public async Task<DeviceDto?> GetDeviceByIdAsync(int id)
    {
        var d = await _deviceRepository.GetByIdAsync(id);
        if (d == null) return null;

        return new DeviceDto
        {
            Id = d.Id,
            Name = d.Name,
            IP = d.IP,
            MAC = d.MAC,
            SerialNumber = d.SerialNumber,
            DeviceType = d.DeviceType.ToString(),
            TCPPort = d.TCPPort,
            Gateway = d.Gateway,
            SubnetMask = d.SubnetMask,
            IsOnline = d.IsOnline,
            DoorCount = GetDoorCount(d.DeviceType.ToString())
        };
    }

    public async Task AddDeviceAsync(DeviceDto dto)
    {
        // Check for duplicate at database level
        var existing = await _deviceRepository.GetBySerialNumberAsync(dto.SerialNumber);
        if (existing != null)
        {
            // Update existing device's IP/MAC/port/network in case they changed
            existing.IP = dto.IP;
            existing.MAC = dto.MAC;
            existing.TCPPort = dto.TCPPort;
            existing.Gateway = dto.Gateway;
            existing.SubnetMask = dto.SubnetMask;
            existing.IsOnline = true;
            await _deviceRepository.UpdateAsync(existing);
            return;
        }

        var device = new Device
        {
            Name = dto.Name,
            IP = dto.IP,
            MAC = dto.MAC,
            SerialNumber = dto.SerialNumber,
            TCPPort = dto.TCPPort,
            Gateway = dto.Gateway,
            SubnetMask = dto.SubnetMask,
            DeviceType = Enum.Parse<Domain.Enums.DeviceType>(dto.DeviceType)
        };
        await _deviceRepository.AddAsync(device);
    }

    public async Task DeleteDeviceAsync(int id)
    {
        await _deviceRepository.DeleteAsync(id);
    }

    public async Task<DeviceDto?> SearchNetworkAsync()
    {
        _sdk.Initialize();
        var deviceInfo = await _sdk.SearchDeviceAsync();
        if (deviceInfo == null) return null;

        // Determine device type from serial number (6th char = door count)
        var deviceType = "CR3222T";
        if (deviceInfo.SerialNumber.Length > 5)
        {
            var doorChar = deviceInfo.SerialNumber[5];
            deviceType = doorChar switch
            {
                '1' => "CR3212T",
                '2' => "CR3222T",
                '4' => "CR3242T",
                _ => "CR3222T"
            };
        }

        return new DeviceDto
        {
            Name = $"Controller {deviceInfo.IP}",
            IP = deviceInfo.IP,
            MAC = deviceInfo.MAC,
            SerialNumber = deviceInfo.SerialNumber,
            TCPPort = deviceInfo.TCPPort,
            Gateway = deviceInfo.Gateway,
            SubnetMask = deviceInfo.SubnetMask,
            DeviceType = deviceType,
            IsOnline = true,
            DoorCount = GetDoorCount(deviceType)
        };
    }

    public async Task<bool> ConnectDeviceAsync(int deviceId)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null) return false;

        _sdk.Initialize();
        var info = ToDeviceInfo(device);
        var doorCount = GetDoorCount(device.DeviceType.ToString());
        _sdk.InitializeDevice(info, doorCount);

        device.IsOnline = true;
        await _deviceRepository.UpdateAsync(device);
        return true;
    }

    public async Task<string> GetDeviceInfoAsync(int deviceId)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null) return string.Empty;

        _sdk.Initialize();
        var info = ToDeviceInfo(device);
        return _sdk.GetDeviceInfo(info);
    }

    public async Task<bool> RemoteOpenDoorAsync(int deviceId, int doorNumber)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null) return false;

        _sdk.Initialize();
        var info = ToDeviceInfo(device);
        // SDK uses 0-indexed door numbers: door 1 = 0, door 2 = 1, etc.
        _sdk.RemoteOpenDoor(info, new[] { doorNumber - 1 });
        return true;
    }

    public async Task<bool> RemoteOpenAllDoorsAsync(int deviceId)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null) return false;

        _sdk.Initialize();
        var info = ToDeviceInfo(device);
        var doorCount = GetDoorCount(device.DeviceType.ToString());
        // SDK uses 0-indexed: pass all door indices in a single call
        var allDoors = Enumerable.Range(0, doorCount).ToArray();
        _sdk.RemoteOpenDoor(info, allDoors);
        return true;
    }

    public async Task<bool> SyncTimeAsync(int deviceId)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null) return false;

        _sdk.Initialize();
        var info = ToDeviceInfo(device);
        _sdk.CalibrateTime(info);
        return true;
    }

    public async Task<bool> RenameDeviceAsync(int deviceId, string newName)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null) return false;

        device.Name = newName;
        await _deviceRepository.UpdateAsync(device);
        return true;
    }

    public async Task<bool> ChangeIPAsync(int deviceId, string newIP, string subnet, string gateway)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "changeip_debug.txt");
        void Log(string msg) => File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");

        try
        {
            File.WriteAllText(logPath, $"=== ChangeIP started at {DateTime.Now} ===\n");
            Log($"deviceId={deviceId}, newIP={newIP}, subnet={subnet}, gateway={gateway}");

            var device = await _deviceRepository.GetByIdAsync(deviceId);
            if (device == null) { Log("Device not found"); return false; }
            Log($"Device loaded: IP={device.IP}, MAC={device.MAC}, SN={device.SerialNumber}, GW={device.Gateway}, SM={device.SubnetMask}");

            _sdk.Initialize();
            Log("SDK initialized");

            var doorCount = GetDoorCount(device.DeviceType.ToString());
            Log($"doorCount={doorCount}");

            // Connect with OLD IP first
            var oldInfo = ToDeviceInfo(device);
            Log($"Calling install with oldIP={oldInfo.IP}");
            _sdk.InitializeDevice(oldInfo, doorCount);
            Log("install() completed");

            // Now call updateIP with new network settings
            var newInfo = ToDeviceInfo(device);
            newInfo.IP = newIP;
            newInfo.SubnetMask = subnet;
            newInfo.Gateway = gateway;
            Log($"Calling updateIP with newIP={newIP}");
            _sdk.UpdateIP(newInfo, doorCount);
            Log("updateIP() completed");

            // Update DB
            device.IP = newIP;
            device.SubnetMask = subnet;
            device.Gateway = gateway;
            await _deviceRepository.UpdateAsync(device);
            Log("DB updated");
            return true;
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] EXCEPTION: {ex}\n");
            throw;
        }
    }

    private static SDK.Models.DeviceInfo ToDeviceInfo(Device device) => new()
    {
        IP = device.IP,
        MAC = device.MAC,
        SerialNumber = device.SerialNumber,
        TCPPort = device.TCPPort,
        UDPPort = device.UDPPort,
        Password = device.Password,
        Gateway = device.Gateway,
        SubnetMask = device.SubnetMask
    };

    private static int GetDoorCount(string deviceType) => deviceType switch
    {
        "CR3212T" => 1,
        "CR3222T" => 2,
        "CR3242T" => 4,
        "CR3216H" => 1,
        "CR3226H" => 2,
        "CR3246H" => 4,
        _ => 2
    };
}
