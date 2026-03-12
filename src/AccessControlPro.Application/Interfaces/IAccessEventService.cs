using AccessControlPro.Application.DTOs;
using AccessControlPro.Domain.Enums;

namespace AccessControlPro.Application.Interfaces;

public interface IAccessEventService
{
    Task<(IEnumerable<AccessEventDto> Items, int TotalCount)> GetEventsPagedAsync(int page, int pageSize, DateTime? from = null, DateTime? to = null, int? doorId = null, string? search = null, RecordType? eventType = null, int? deviceId = null);
    Task SaveEventAsync(int doorId, int? cardId, int recordType, int eventCode, DateTime timestamp, string details);
    Task<int> CleanupOldEventsAsync(int monthsToKeep = 6);
    Task<int> FetchAndSaveRecordsAsync(int deviceId, DateTime? fromDate = null);
}
