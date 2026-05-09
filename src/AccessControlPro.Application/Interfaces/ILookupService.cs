using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Application.Interfaces;

public interface ILookupService
{
    Task<List<LookupItem>> GetByCategoryAsync(string category);
    Task AddAsync(LookupItem item);
    Task UpdateAsync(LookupItem item);
    Task DeleteAsync(int id);

    /// <summary>
    /// Source-of-truth for subscription plan dropdowns in player dialogs.
    /// Reads the SubscriptionPlans table that the Admin Panel writes to, so what the admin
    /// configures actually shows up in the main app.
    /// </summary>
    Task<List<SubscriptionPlan>> GetActiveSubscriptionPlansAsync();
}
