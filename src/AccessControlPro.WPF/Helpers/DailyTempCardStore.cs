using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Tracks daily-pass temporary cards that are currently "out" with a guest, so the
/// cashier can see who still holds a card and mark it returned. Local cashier state
/// only (the income itself is recorded in the Transactions table). Stored as JSON
/// next to the app so it survives restarts.
/// </summary>
public static class DailyTempCardStore
{
    public class TempCard
    {
        public string CardNumber { get; set; } = "";
        public decimal Price { get; set; }
        public DateTime IssuedAt { get; set; }
    }

    private static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "daily_temp_cards.json");

    /// <summary>
    /// A daily-pass card auto-frees itself after this long even if the cashier never clicks
    /// "Return" — the bracelet is back in the pool and the same number can be issued again.
    /// The gate already auto-rejects the card after its issue night (ValidTo = that day 23:59),
    /// so this only clears the local "still out" flag. Default: 24 hours.
    /// </summary>
    public static readonly TimeSpan AutoReturnAfter = TimeSpan.FromHours(24);

    /// <summary>Cards still out — those issued within the last <see cref="AutoReturnAfter"/>.
    /// Anything older is auto-returned (pruned from disk) so its number is re-issuable.</summary>
    public static List<TempCard> LoadOut()
    {
        var all = LoadRaw();
        var cutoff = DateTime.Now - AutoReturnAfter;
        var active = all.Where(c => c.IssuedAt > cutoff).ToList();
        if (active.Count != all.Count) Save(active);   // drop expired entries from disk
        return active;
    }

    private static List<TempCard> LoadRaw()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<TempCard>();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<TempCard>>(json) ?? new List<TempCard>();
        }
        catch { return new List<TempCard>(); }
    }

    private static void Save(List<TempCard> cards)
    {
        try
        {
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(cards, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best-effort local state */ }
    }

    /// <summary>True if this card number is already out (prevents double-issue).</summary>
    public static bool IsOut(string cardNumber) =>
        LoadOut().Any(c => c.CardNumber == cardNumber.Trim());

    public static void Issue(string cardNumber, decimal price)
    {
        var cards = LoadOut();
        cardNumber = cardNumber.Trim();
        if (cards.Any(c => c.CardNumber == cardNumber)) return;
        cards.Add(new TempCard { CardNumber = cardNumber, Price = price, IssuedAt = DateTime.Now });
        Save(cards);
    }

    public static void Return(string cardNumber)
    {
        var cards = LoadOut();
        cards.RemoveAll(c => c.CardNumber == cardNumber.Trim());
        Save(cards);
    }
}
