using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface ILookupRepository
{
    Task<List<LookupItem>> GetByCategoryAsync(string category);
    Task<LookupItem?> GetByIdAsync(int id);
    Task AddAsync(LookupItem item);
    Task UpdateAsync(LookupItem item);
    Task DeleteAsync(int id);

    /// <summary>
    /// Returns active subscription plans defined in the Admin Panel ("Subscription Plans" page).
    /// Source-of-truth for the dropdown shown in Add/Edit/Renew player dialogs.
    /// </summary>
    Task<List<SubscriptionPlan>> GetActiveSubscriptionPlansAsync();
}
