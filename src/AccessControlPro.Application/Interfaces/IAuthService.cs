using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Application.Interfaces;

public enum LoginError
{
    None,
    UserNotFound,
    WrongPassword,
    AccountDisabled,
    AccountLockedOut
}

public record LoginResult(AppUser? User, LoginError Error);

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, string password);
    Task SeedAdminAsync();
    Task<IEnumerable<AppUser>> GetAllUsersAsync();
    Task CreateUserAsync(string username, string password, string displayName, string role, string permissions = "");
    Task UpdateUserAsync(int userId, string displayName, string role, bool isActive, string permissions = "");
    Task ResetPasswordAsync(int userId, string newPassword);
    Task ChangePasswordAsync(string username, string currentPassword, string newPassword);
    bool IsDefaultPassword(string passwordHash);
    Task<AppUser?> GetUserByUsernameAsync(string username);
}
