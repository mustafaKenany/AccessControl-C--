using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _repo;

    public SupplierService(ISupplierRepository repo) => _repo = repo;

    public async Task<IEnumerable<SupplierDto>> GetAllAsync()
    {
        var suppliers = await _repo.GetAllActiveAsync();
        return suppliers.Select(s => new SupplierDto
        {
            Id = s.Id,
            Name = s.Name,
            Phone = s.Phone,
            Address = s.Address,
            ContactPerson = s.ContactPerson
        });
    }

    public async Task AddAsync(SupplierDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Supplier name is required.");

        await _repo.AddAsync(new Supplier
        {
            Name = dto.Name,
            Phone = dto.Phone,
            Address = dto.Address,
            ContactPerson = dto.ContactPerson
        });
    }

    public async Task UpdateAsync(SupplierDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Supplier name is required.");

        var supplier = await _repo.GetByIdAsync(dto.Id)
            ?? throw new InvalidOperationException($"Supplier with ID {dto.Id} not found.");

        supplier.Name = dto.Name;
        supplier.Phone = dto.Phone;
        supplier.Address = dto.Address;
        supplier.ContactPerson = dto.ContactPerson;
        await _repo.UpdateAsync(supplier);
    }

    public async Task DeleteAsync(int id)
    {
        await _repo.DeleteAsync(id);
    }
}
