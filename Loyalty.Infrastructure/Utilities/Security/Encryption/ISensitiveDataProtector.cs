namespace Loyalty.Infrastructure.Utilities.Security.Encryption
{
    /// <summary>
    /// Encrypts/decrypts sensitive data (e.g. POS API tokens) and computes token hashes for validation.
    /// </summary>
    public interface ISensitiveDataProtector
    {
        /// <summary>Encrypts plain text for storage in DB. Returns base64 string.</summary>
        string Encrypt(string plainText);

        /// <summary>Decrypts previously encrypted text. Returns null if decryption fails.</summary>
        string? Decrypt(string encryptedText);

        /// <summary>Computes SHA256 hash of token for storage (used for wallet token validation).</summary>
        string ComputeTokenHash(string token);

        /// <summary>Verifies a token against stored hash.</summary>
        bool VerifyTokenHash(string token, string storedHash);
    }
}
