using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class DeletedEmployeeService : IDeletedEmployeeService
{
    private readonly IDeletedEmployeeRepository _repository;

    public DeletedEmployeeService(IDeletedEmployeeRepository repository)
    {
        _repository = repository;
    }

    public async Task<(IEnumerable<DeletedEmployeeDto> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(page, pageSize, search);
        var dtos = items.Select(e => new DeletedEmployeeDto
        {
            Id = e.Id,
            OriginalId = e.OriginalId,
            FullNameEn = e.FullNameEn,
            FullNameAr = e.FullNameAr,
            CardNo = e.CardNo,
            SubscriptionType = e.SubscriptionType,
            Phone = e.Phone,
            PhotoData = e.PhotoData,
            SubscriptionFee = e.SubscriptionFee,
            AmountPaid = e.AmountPaid,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            Notes = e.Notes,
            DeleteReason = e.DeleteReason,
            DeletedBy = e.DeletedBy,
            DeletedAt = e.DeletedAt,
            OriginalCreatedAt = e.OriginalCreatedAt
        });
        return (dtos, totalCount);
    }
}
