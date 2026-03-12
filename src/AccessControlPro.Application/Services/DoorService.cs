using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.SDK.Wrapper;

namespace AccessControlPro.Application.Services;

public class DoorService : IDoorService
{
    private readonly IDoorRepository _doorRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IAccessControlSdk _sdk;

    public DoorService(IDoorRepository doorRepository, IDeviceRepository deviceRepository, IAccessControlSdk sdk)
    {
        _doorRepository = doorRepository;
        _deviceRepository = deviceRepository;
        _sdk = sdk;
    }

    public async Task<IEnumerable<DoorDto>> GetAllDoorsAsync()
    {
        // Auto-create Door entities for devices that don't have them yet
        await EnsureDoorsExistAsync();

        var doors = await _doorRepository.GetAllWithDeviceAsync();
        return doors.Select(d => new DoorDto
        {
            Id = d.Id,
            DeviceId = d.DeviceId,
            DeviceName = d.Device?.Name ?? "",
            DeviceIP = d.Device?.IP ?? "",
            DoorNumber = d.DoorNumber,
            Name = d.Name,
            Status = d.Status.ToString(),
            IsLocked = d.IsLocked
        });
    }

    public async Task<bool> OpenDoorAsync(int doorId)
    {
        var door = await _doorRepository.GetByIdWithDeviceAsync(doorId);
        if (door == null) return false;

        if (door.Device == null) return false;
        _sdk.Initialize();
        var info = ToDeviceInfo(door.Device);
        // SDK uses 0-indexed door numbers
        _sdk.RemoteOpenDoor(info, new[] { door.DoorNumber - 1 });
        return true;
    }

    public async Task<bool> CloseDoorAsync(int doorId)
    {
        var door = await _doorRepository.GetByIdWithDeviceAsync(doorId);
        if (door == null) return false;

        if (door.Device == null) return false;
        _sdk.Initialize();
        var info = ToDeviceInfo(door.Device);
        _sdk.RemoteCloseDoor(info, new[] { door.DoorNumber - 1 });
        return true;
    }

    public async Task<bool> SetDoorDelayAsync(int doorId, int delaySeconds)
    {
        var door = await _doorRepository.GetByIdWithDeviceAsync(doorId);
        if (door == null) return false;

        if (door.Device == null) return false;
        _sdk.Initialize();
        var info = ToDeviceInfo(door.Device);
        // SDK uses 0-indexed door numbers
        _sdk.SetDoorDelay(info, door.DoorNumber - 1, delaySeconds);
        return true;
    }

    public async Task<bool> SetDoorPasswordAsync(int doorId, string password)
    {
        var door = await _doorRepository.GetByIdWithDeviceAsync(doorId);
        if (door == null) return false;

        if (door.Device == null) return false;
        _sdk.Initialize();
        var info = ToDeviceInfo(door.Device);
        _sdk.SetDoorPassword(info, password);
        return true;
    }

    public async Task<bool> RenameDoorAsync(int doorId, string newName)
    {
        var door = await _doorRepository.GetByIdWithDeviceAsync(doorId);
        if (door == null) return false;

        door.Name = newName;
        await _doorRepository.UpdateAsync(door);
        return true;
    }

    private async Task EnsureDoorsExistAsync()
    {
        var devices = await _deviceRepository.GetAllAsync();
        foreach (var device in devices)
        {
            var existingDoors = (await _doorRepository.GetByDeviceIdAsync(device.Id)).ToList();
            var existingNumbers = existingDoors.Select(d => d.DoorNumber).ToHashSet();
            var expectedCount = GetDoorCount(device.DeviceType.ToString());

            var newDoors = new List<Door>();
            for (int i = 1; i <= expectedCount; i++)
            {
                if (!existingNumbers.Contains(i))
                {
                    newDoors.Add(new Door
                    {
                        DeviceId = device.Id,
                        DoorNumber = i,
                        Name = $"{device.Name} - Door {i}"
                    });
                }
            }
            if (newDoors.Count > 0)
                await _doorRepository.AddRangeAsync(newDoors);
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
