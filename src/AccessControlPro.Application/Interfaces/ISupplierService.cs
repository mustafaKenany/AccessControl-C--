using AccessControlPro.Application.DTOs;

namespace AccessControlPro.Application.Interfaces;

public interface ISupplierService
{
    Task<IEnumerable<SupplierDto>> GetAllAsync();
    Task AddAsync(SupplierDto dto);
    Task UpdateAsync(SupplierDto dto);
    Task DeleteAsync(int id);
}
