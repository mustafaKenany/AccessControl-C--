using AccessControlPro.Application.DTOs;

namespace AccessControlPro.Application.Interfaces;

public interface IDoorService
{
    Task<IEnumerable<DoorDto>> GetAllDoorsAsync();
    Task<bool> OpenDoorAsync(int doorId);
    Task<bool> CloseDoorAsync(int doorId);
    Task<bool> SetDoorDelayAsync(int doorId, int delaySeconds);
    Task<bool> SetDoorPasswordAsync(int doorId, string password);
    Task<bool> RenameDoorAsync(int doorId, string newName);
}
