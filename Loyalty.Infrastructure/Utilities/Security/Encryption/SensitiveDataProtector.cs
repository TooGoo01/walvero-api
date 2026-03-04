using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Loyalty.Infrastructure.Utilities.Security.Encryption
{
    public class SensitiveDataProtector : ISensitiveDataProtector
    {
        private readonly byte[] _key;

        public SensitiveDataProtector(IConfiguration configuration)
        {
            var keyString = configuration["Encryption:Key"]
                ?? throw new InvalidOperationException("Encryption:Key is not configured in appsettings.json");

            // SHA256 hash to ensure exactly 32 bytes for AES-256
            _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyString));
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // IV (16 bytes) + CipherText
            var result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        public string? Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return null;

            try
            {
                var fullBytes = Convert.FromBase64String(encryptedText);
                if (fullBytes.Length < 17) // IV (16) + at least 1 byte
                    return null;

                using var aes = Aes.Create();
                aes.Key = _key;

                var iv = new byte[16];
                Buffer.BlockCopy(fullBytes, 0, iv, 0, 16);
                aes.IV = iv;

                var cipherBytes = new byte[fullBytes.Length - 16];
                Buffer.BlockCopy(fullBytes, 16, cipherBytes, 0, cipherBytes.Length);

                using var decryptor = aes.CreateDecryptor();
                var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return null;
            }
        }

        public string ComputeTokenHash(string token)
        {
            if (string.IsNullOrEmpty(token))
                return string.Empty;

            var bytes = Encoding.UTF8.GetBytes(token);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public bool VerifyTokenHash(string token, string storedHash)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(storedHash))
                return false;

            var computed = ComputeTokenHash(token);
            return string.Equals(computed, storedHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
