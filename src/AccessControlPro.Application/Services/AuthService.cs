using System.Collections.Concurrent;
using System.IO;
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

    // File log — captured in diagnostics bundles. Username is logged, password
    // never is, not even in failure paths.
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "auth_log.txt");
    private static void Log(string msg) => RollingLogFile.Append(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            Log("Login rejected: empty username or password");
            return new LoginResult(null, LoginError.UserNotFound);
        }

        username = username.Trim().ToLowerInvariant();

        // Check if account is locked out due to too many failed attempts
        if (_failedAttempts.TryGetValue(username, out var attempts))
        {
            if (attempts.Count >= MaxFailedAttempts && DateTime.UtcNow - attempts.LastAttempt < LockoutDuration)
            {
                Log($"Login rejected: account locked (user={username}, failed={attempts.Count})");
                return new LoginResult(null, LoginError.AccountLockedOut);
            }

            // Reset if lockout expired
            if (DateTime.UtcNow - attempts.LastAttempt >= LockoutDuration)
                _failedAttempts.TryRemove(username, out _);
        }

        var user = await _userRepository.GetByUsernameAsync(username);
        if (user == null)
        {
            RecordFailedAttempt(username);
            Log($"Login failed: user not found (user={username})");
            return new LoginResult(null, LoginError.UserNotFound);
        }

        if (!user.IsActive)
        {
            Log($"Login rejected: account disabled (user={username})");
            return new LoginResult(null, LoginError.AccountDisabled);
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            RecordFailedAttempt(username);
            Log($"Login failed: wrong password (user={username})");
            return new LoginResult(null, LoginError.WrongPassword);
        }

        // Clear failed attempts on successful login
        _failedAttempts.TryRemove(username, out _);
        Log($"Login OK (user={username}, role={user.Role})");
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
        Log($"CreateUser OK (user={username}, role={role})");
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
        Log($"UpdateUser OK (id={userId}, user={user.Username}, role={role}, active={isActive})");
    }

    public async Task ResetPasswordAsync(int userId, string newPassword)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user);
        Log($"ResetPassword OK (id={userId}, user={user.Username})");
    }

    public async Task ChangePasswordAsync(string username, string currentPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            throw new ArgumentException("New password must be at least 6 characters.");

        var user = await _userRepository.GetByUsernameAsync(username)
            ?? throw new InvalidOperationException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            Log($"ChangePassword failed: current password wrong (user={username})");
            throw new InvalidOperationException("Current password is incorrect.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user);
        Log($"ChangePassword OK (user={username})");
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
        Log($"DeleteUser OK (id={userId}, user={user.Username})");
    }
}
