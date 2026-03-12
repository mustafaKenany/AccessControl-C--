namespace AccessControlPro.Infrastructure.DeviceManagement;

/// <summary>
/// Encrypts/decrypts device passwords using DPAPI (Windows Data Protection API)
/// Passwords stored plaintext in DB is a security risk - this service provides encryption
/// </summary>
public interface IDevicePasswordEncryption
{
    /// <summary>
    /// Encrypts a device password. Returns encrypted string safe to store in DB.
    /// </summary>
    string EncryptPassword(string plainPassword);

    /// <summary>
    /// Decrypts a device password from DB. Returns plaintext for SDK communication.
    /// </summary>
    string DecryptPassword(string encryptedPassword);
}

public class DevicePasswordEncryption : IDevicePasswordEncryption
{
    public string EncryptPassword(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword))
            throw new ArgumentException("Password cannot be null or empty.", nameof(plainPassword));

        try
        {
            var dataToEncrypt = System.Text.Encoding.UTF8.GetBytes(plainPassword);
            var encryptedData = System.Security.Cryptography.ProtectedData.Protect(
                dataToEncrypt,
                null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(encryptedData);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to encrypt device password.", ex);
        }
    }

    public string DecryptPassword(string encryptedPassword)
    {
        if (string.IsNullOrEmpty(encryptedPassword))
            throw new ArgumentException("Encrypted password cannot be null or empty.", nameof(encryptedPassword));

        try
        {
            var encryptedData = Convert.FromBase64String(encryptedPassword);
            var decryptedData = System.Security.Cryptography.ProtectedData.Unprotect(
                encryptedData,
                null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);

            return System.Text.Encoding.UTF8.GetString(decryptedData);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to decrypt device password.", ex);
        }
    }
}
