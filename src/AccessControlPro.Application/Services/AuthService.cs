using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        var user = await _userRepository.GetByUsernameAsync(username);
        if (user == null)
            return new LoginResult(null, LoginError.UserNotFound);

        if (!user.IsActive)
            return new LoginResult(null, LoginError.AccountDisabled);

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return new LoginResult(null, LoginError.WrongPassword);

        return new LoginResult(user, LoginError.None);
    }

    public async Task SeedAdminAsync()
    {
        var existing = await _userRepository.GetByUsernameAsync("admin");
        if (existing != null) return;

        var admin = new AppUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"),
            DisplayName = "Administrator",
            Role = "Admin",
            IsActive = true
        };
        await _userRepository.AddAsync(admin);
    }
}
