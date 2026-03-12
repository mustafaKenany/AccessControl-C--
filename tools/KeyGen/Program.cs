using System.Security.Cryptography;
using System.Text;

Console.Title = "AccessControlPro - License Key Generator";
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("╔════════════════════════════════════════════════╗");
Console.WriteLine("║   AccessControlPro License Key Generator       ║");
Console.WriteLine("║   KEEP THIS TOOL PRIVATE - DO NOT DISTRIBUTE  ║");
Console.WriteLine("╚════════════════════════════════════════════════╝");
Console.ResetColor();
Console.WriteLine();

while (true)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("Enter Machine ID (or 'quit' to exit): ");
    Console.ResetColor();
    var machineId = Console.ReadLine()?.Trim().ToUpperInvariant();

    if (string.IsNullOrEmpty(machineId) || machineId == "QUIT" || machineId == "Q")
        break;

    // Validate format: XXXX-XXXX-XXXX-XXXX
    if (machineId.Replace("-", "").Length != 16)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Invalid Machine ID format. Expected: XXXX-XXXX-XXXX-XXXX");
        Console.ResetColor();
        Console.WriteLine();
        continue;
    }

    Console.Write("Duration in months (default 6): ");
    var monthsInput = Console.ReadLine()?.Trim();
    int months = 6;
    if (!string.IsNullOrEmpty(monthsInput) && int.TryParse(monthsInput, out var m) && m > 0)
        months = m;

    try
    {
        var key = GenerateKey(machineId, months);
        var expiryDate = DateTime.Today.AddMonths(months);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╔════════════════════════════════════════════════╗");
        Console.WriteLine($"  Machine ID:  {machineId}");
        Console.WriteLine($"  License Key: {key}");
        Console.WriteLine($"  Expires:     {expiryDate:yyyy-MM-dd}");
        Console.WriteLine($"  Duration:    {months} months");
        Console.WriteLine("╚════════════════════════════════════════════════╝");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {ex.Message}");
        Console.ResetColor();
    }

    Console.WriteLine();
}

Console.WriteLine("Goodbye.");

// ── Key generation logic (must match LicenseService exactly) ──

static string GenerateKey(string machineId, int months)
{
    var expiryDate = DateTime.Today.AddMonths(months);
    var epoch = new DateTime(2024, 1, 1);
    var secretKey = SHA256.HashData(
        Encoding.UTF8.GetBytes("ACP-2024-GymAccess-LicenseKey-X9K2M"));

    int days = (int)(expiryDate.Date - epoch).TotalDays;
    if (days < 0 || days > 65535)
        throw new ArgumentException("Expiry date out of range.");

    var payload = new byte[10];
    payload[0] = (byte)(days >> 8);
    payload[1] = (byte)(days & 0xFF);

    var message = Encoding.UTF8.GetBytes($"{machineId.ToUpperInvariant()}|{expiryDate:yyyyMMdd}");
    var hmac = HMACSHA256.HashData(secretKey, message);
    Array.Copy(hmac, 0, payload, 2, 8);

    var hex = Convert.ToHexString(payload).ToUpperInvariant();
    return $"{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}";
}
