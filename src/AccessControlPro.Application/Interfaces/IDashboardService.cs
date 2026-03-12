using AccessControlPro.Application.DTOs;

namespace AccessControlPro.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardDataAsync();
}
