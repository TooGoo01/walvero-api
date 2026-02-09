namespace Loyalty.Business.Services.Abstractions.Sequences
{
    /// <summary>
    /// Service for generating unique WalveroId for users
    /// Format: {countryCode}1{seq:D5}
    /// Example: 994100001, 994100002, 336100001, ...
    /// </summary>
    public interface IWalveroIdGenerator
    {
        /// <summary>
        /// Generates a unique WalveroId for a country code
        /// </summary>
        /// <param name="countryCode">Country code (e.g., 994, 336)</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Generated WalveroId</returns>
        Task<long> GenerateWalveroIdAsync(int countryCode, CancellationToken ct = default);

        /// <summary>
        /// Extracts country code from phone number and validates it against database
        /// Supports formats: +994, 994, 0 (with country code)
        /// </summary>
        /// <param name="phoneNumber">Phone number</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Country code or null if not found or not valid in database</returns>
        Task<int?> ExtractCountryCodeAsync(string? phoneNumber, CancellationToken ct = default);
    }
}

