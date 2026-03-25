using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IPosShiftRepository
{
    Task<PosShift?> GetOpenShiftAsync();
    Task<List<PosShift>> GetAllOpenShiftsAsync();
    Task AddAsync(PosShift shift);
    Task UpdateAsync(PosShift shift);
    Task UpdateRangeAsync(IEnumerable<PosShift> shifts);
}
