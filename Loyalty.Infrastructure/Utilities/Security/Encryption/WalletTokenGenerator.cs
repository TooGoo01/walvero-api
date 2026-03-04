using System.Security.Cryptography;

namespace Loyalty.Infrastructure.Utilities.Security.Encryption
{
    public class WalletTokenGenerator : IWalletTokenGenerator
    {
        private const int TokenByteLength = 32;
        private readonly ISensitiveDataProtector _protector;

        public WalletTokenGenerator(ISensitiveDataProtector protector)
        {
            _protector = protector;
        }

        public (string Token, string Hash) Generate()
        {
            var bytes = new byte[TokenByteLength];
            RandomNumberGenerator.Fill(bytes);
            var token = Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
            var hash = _protector.ComputeTokenHash(token);
            return (token, hash);
        }
    }
}
