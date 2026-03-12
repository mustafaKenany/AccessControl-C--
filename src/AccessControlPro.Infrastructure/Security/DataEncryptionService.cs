using System.Security.Cryptography;

namespace AccessControlPro.Infrastructure.Security;

/// <summary>
/// Encrypts sensitive data at rest.
/// Used for: photos, personal data, etc.
/// Supports GDPR compliance requirements.
/// </summary>
public interface IDataEncryptionService
{
    /// <summary>Encrypt binary data (photos, files)</summary>
    byte[] EncryptBinary(byte[] data, string encryptionKey);

    /// <summary>Decrypt binary data</summary>
    byte[] DecryptBinary(byte[] encryptedData, string encryptionKey);

    /// <summary>Encrypt text</summary>
    string EncryptText(string plainText, string encryptionKey);

    /// <summary>Decrypt text</summary>
    string DecryptText(string encryptedText, string encryptionKey);

    /// <summary>Hash sensitive data (one-way, for verification)</summary>
    string HashData(string data, string salt = "");

    /// <summary>Verify hashed data</summary>
    bool VerifyHashedData(string data, string hash, string salt = "");

    /// <summary>Generate random encryption key</summary>
    string GenerateEncryptionKey();
}

public class DataEncryptionService : IDataEncryptionService
{
    private const int KeySize = 256; // AES-256
    private const int IvSize = 128;  // AES block size

    public byte[] EncryptBinary(byte[] data, string encryptionKey)
    {
        if (data == null || data.Length == 0)
            throw new ArgumentException("Data cannot be empty", nameof(data));

        if (string.IsNullOrWhiteSpace(encryptionKey))
            throw new ArgumentException("Encryption key cannot be empty", nameof(encryptionKey));

        try
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                // Derive key from password
                using (var kdf = new Rfc2898DeriveBytes(encryptionKey, 16, 10000, HashAlgorithmName.SHA256))
                {
                    aes.Key = kdf.GetBytes(KeySize / 8);
                }

                // Generate random IV
                aes.GenerateIV();

                using (var cipher = aes.CreateEncryptor())
                {
                    // Encrypt data
                    var encryptedData = cipher.TransformFinalBlock(data, 0, data.Length);

                    // Combine IV + CipherText
                    var result = new byte[aes.IV.Length + encryptedData.Length];
                    Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                    Buffer.BlockCopy(encryptedData, 0, result, aes.IV.Length, encryptedData.Length);

                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to encrypt data", ex);
        }
    }

    public byte[] DecryptBinary(byte[] encryptedData, string encryptionKey)
    {
        if (encryptedData == null || encryptedData.Length == 0)
            throw new ArgumentException("Encrypted data cannot be empty", nameof(encryptedData));

        if (string.IsNullOrWhiteSpace(encryptionKey))
            throw new ArgumentException("Encryption key cannot be empty", nameof(encryptionKey));

        try
        {
            using (var aes = Aes.Create())
            {
                aes.KeySize = KeySize;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                // Derive key from password
                using (var kdf = new Rfc2898DeriveBytes(encryptionKey, 16, 10000, HashAlgorithmName.SHA256))
                {
                    aes.Key = kdf.GetBytes(KeySize / 8);
                }

                // Extract IV from beginning of encrypted data
                var iv = new byte[aes.IV.Length];
                Buffer.BlockCopy(encryptedData, 0, iv, 0, iv.Length);
                aes.IV = iv;

                // Extract cipher text
                var cipherText = new byte[encryptedData.Length - iv.Length];
                Buffer.BlockCopy(encryptedData, iv.Length, cipherText, 0, cipherText.Length);

                using (var decipher = aes.CreateDecryptor())
                {
                    return decipher.TransformFinalBlock(cipherText, 0, cipherText.Length);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to decrypt data", ex);
        }
    }

    public string EncryptText(string plainText, string encryptionKey)
    {
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = EncryptBinary(plainBytes, encryptionKey);
        return Convert.ToBase64String(encryptedBytes);
    }

    public string DecryptText(string encryptedText, string encryptionKey)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedText);
        var plainBytes = DecryptBinary(encryptedBytes, encryptionKey);
        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }

    public string HashData(string data, string salt = "")
    {
        var saltBytes = string.IsNullOrEmpty(salt)
            ? System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())
            : System.Text.Encoding.UTF8.GetBytes(salt);

        using (var pbkdf2 = new Rfc2898DeriveBytes(data, saltBytes, 10000, HashAlgorithmName.SHA256))
        {
            var hash = pbkdf2.GetBytes(32);
            // Return salt + hash combined
            var result = new byte[saltBytes.Length + hash.Length];
            Buffer.BlockCopy(saltBytes, 0, result, 0, saltBytes.Length);
            Buffer.BlockCopy(hash, 0, result, saltBytes.Length, hash.Length);
            return Convert.ToBase64String(result);
        }
    }

    public bool VerifyHashedData(string data, string hash, string salt = "")
    {
        try
        {
            var hashBytes = Convert.FromBase64String(hash);

            // For simplicity, re-hash and compare
            // In production, should use bcrypt or Argon2
            var saltBytes = string.IsNullOrEmpty(salt)
                ? hashBytes.Take(16).ToArray() // Extract original salt
                : System.Text.Encoding.UTF8.GetBytes(salt);

            using (var pbkdf2 = new Rfc2898DeriveBytes(data, saltBytes, 10000, HashAlgorithmName.SHA256))
            {
                var newHash = pbkdf2.GetBytes(32);
                var storedHash = hashBytes.Skip(16).ToArray(); // Skip salt to get hash
                return newHash.SequenceEqual(storedHash);
            }
        }
        catch
        {
            return false;
        }
    }

    public string GenerateEncryptionKey()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            var keyBytes = new byte[32]; // 256 bits
            rng.GetBytes(keyBytes);
            return Convert.ToBase64String(keyBytes);
        }
    }
}
