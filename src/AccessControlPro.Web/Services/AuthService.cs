using AccessControlPro.Domain.Entities;
using AccessControlPro.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Web.Services;

public class WebAuthService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public WebAuthService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Owner login using username + password (BCrypt verified against Users table).
    /// </summary>
    public async Task<AuthResult> OwnerLoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return AuthResult.Failed("Username and password are required.");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CloudDbContext>();

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null)
            return AuthResult.Failed("Invalid username or password.");

        try
        {
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return AuthResult.Failed("Invalid username or password.");
        }
        catch
        {
            return AuthResult.Failed("Invalid username or password.");
        }

        return AuthResult.Success(user.DisplayName, "Owner", user.Id);
    }

    /// <summary>
    /// Player login using phone number + last 4 digits of card number.
    /// </summary>
    public async Task<AuthResult> PlayerLoginAsync(string phone, string cardLast4)
    {
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(cardLast4))
            return AuthResult.Failed("Phone and card digits are required.");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CloudDbContext>();

        var player = await db.Players
            .FirstOrDefaultAsync(p => p.Phone == phone);

        if (player == null)
            return AuthResult.Failed("Player not found. Check your phone number.");

        // Verify last 4 digits of card number
        var card = await db.AccessCards
            .FirstOrDefaultAsync(c => c.EmployeeId == player.Id && c.IsActive);

        if (card == null)
            return AuthResult.Failed("No active card found for this player.");

        var last4 = card.CardNumber.Length >= 4
            ? card.CardNumber[^4..]
            : card.CardNumber;

        if (!string.Equals(last4, cardLast4.Trim(), StringComparison.OrdinalIgnoreCase))
            return AuthResult.Failed("Invalid card digits.");

        return AuthResult.Success(player.FullNameEn, "Player", player.Id);
    }
}

public class AuthResult
{
    public bool IsAuthenticated { get; set; }
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = ""; // "Owner" or "Player"
    public int UserId { get; set; }
    public string Error { get; set; } = "";

    public static AuthResult Success(string displayName, string role, int userId)
        => new() { IsAuthenticated = true, DisplayName = displayName, Role = role, UserId = userId };

    public static AuthResult Failed(string error)
        => new() { IsAuthenticated = false, Error = error };
}
