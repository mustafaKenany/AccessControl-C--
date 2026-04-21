using System.Collections.Concurrent;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using BCrypt.Net;

namespace AccessControlPro.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;

    // Brute force protection: track failed login attempts per username
    private static readonly ConcurrentDictionary<string, (int Count, DateTime LastAttempt)> _failedAttempts = new();
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new LoginResult(null, LoginError.UserNotFound);

        username = username.Trim().ToLowerInvariant();

        // Check if account is locked out due to too many failed attempts
        if (_failedAttempts.TryGetValue(username, out var attempts))
        {
            if (attempts.Count >= MaxFailedAttempts && DateTime.UtcNow - attempts.LastAttempt < LockoutDuration)
                return new LoginResult(null, LoginError.AccountLockedOut);

            // Reset if lockout expired
            if (DateTime.UtcNow - attempts.LastAttempt >= LockoutDuration)
                _failedAttempts.TryRemove(username, out _);
        }

        var user = await _userRepository.GetByUsernameAsync(username);
        if (user == null)
        {
            RecordFailedAttempt(username);
            return new LoginResult(null, LoginError.UserNotFound);
        }

        if (!user.IsActive)
            return new LoginResult(null, LoginError.AccountDisabled);

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            RecordFailedAttempt(username);
            return new LoginResult(null, LoginError.WrongPassword);
        }

        // Clear failed attempts on successful login
        _failedAttempts.TryRemove(username, out _);
        return new LoginResult(user, LoginError.None);
    }

    private static void RecordFailedAttempt(string username)
    {
        _failedAttempts.AddOrUpdate(username,
            _ => (1, DateTime.UtcNow),
            (_, existing) => (existing.Count + 1, DateTime.UtcNow));
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

    public async Task<IEnumerable<AppUser>> GetAllUsersAsync()
    {
        return await _userRepository.GetAllAsync();
    }

    public async Task CreateUserAsync(string username, string password, string displayName, string role, string permissions = "")
    {
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6) throw new ArgumentException("Password must be at least 6 characters.");
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.");

        username = username.Trim();
        displayName = displayName.Trim();

        var existing = await _userRepository.GetByUsernameAsync(username);
        if (existing != null)
            throw new InvalidOperationException($"Username '{username}' already exists.");

        var user = new AppUser
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = displayName,
            Role = role,
            IsActive = true,
            Permissions = permissions
        };
        await _userRepository.AddAsync(user);
    }

    public async Task UpdateUserAsync(int userId, string displayName, string role, bool isActive, string permissions = "")
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        user.DisplayName = displayName;
        user.Role = role;
        user.IsActive = isActive;
        user.Permissions = permissions;
        await _userRepository.UpdateAsync(user);
    }

    public async Task ResetPasswordAsync(int userId, string newPassword)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user);
    }

    public async Task ChangePasswordAsync(string username, string currentPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            throw new ArgumentException("New password must be at least 6 characters.");

        var user = await _userRepository.GetByUsernameAsync(username)
            ?? throw new InvalidOperationException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw new InvalidOperationException("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user);
    }

    public bool IsDefaultPassword(string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify("123456", passwordHash);
    }

    public async Task<AppUser?> GetUserByUsernameAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        return await _userRepository.GetByUsernameAsync(username.Trim().ToLowerInvariant());
    }

    public async Task DeleteUserAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot delete the default admin account.");

        await _userRepository.DeleteAsync(userId);
    }
}
