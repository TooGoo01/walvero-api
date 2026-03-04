namespace Loyalty.Infrastructure.Utilities.Security.Encryption
{
    /// <summary>
    /// Generates cryptographically random wallet tokens for POS webhook authentication.
    /// </summary>
    public interface IWalletTokenGenerator
    {
        /// <summary>
        /// Generates a new wallet token and its hash.
        /// Token should be shown to user once for copy; only hash is stored in DB.
        /// </summary>
        (string Token, string Hash) Generate();
    }
}
