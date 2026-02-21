using Microsoft.AspNetCore.DataProtection;

namespace Loyalty.Infrastructure.Utilities.Security.Encryption
{
    public class SensitiveDataProtector : ISensitiveDataProtector
    {
        private const string Purpose = "LoyaltyApi.TenantIntegration.Tokens";
        private readonly IDataProtector _protector;

        public SensitiveDataProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(Purpose);
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            var bytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            var protectedBytes = _protector.Protect(bytes);
            return Convert.ToBase64String(protectedBytes);
        }

        public string? Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return null;

            try
            {
                var bytes = Convert.FromBase64String(encryptedText);
                var unprotectedBytes = _protector.Unprotect(bytes);
                return System.Text.Encoding.UTF8.GetString(unprotectedBytes);
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

            var bytes = System.Text.Encoding.UTF8.GetBytes(token);
            var hash = System.Security.Cryptography.SHA256.HashData(bytes);
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
