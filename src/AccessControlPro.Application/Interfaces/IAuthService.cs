using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Application.Interfaces;

public enum LoginError
{
    None,
    UserNotFound,
    WrongPassword,
    AccountDisabled
}

public record LoginResult(AppUser? User, LoginError Error);

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, string password);
    Task SeedAdminAsync();
}
