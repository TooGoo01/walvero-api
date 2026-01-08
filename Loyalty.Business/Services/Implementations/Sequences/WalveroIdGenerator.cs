using Loyalty.Business.Services.Abstractions.Sequences;
using Loyalty.DataAccess.Repositories.Abstractions.Sequences;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Loyalty.Business.Services.Implementations.Sequences
{
    /// <summary>
    /// Service for generating unique WalveroId using atomic sequence generation
    /// Format: {countryCode}1{seq:D5}
    /// </summary>
    public class WalveroIdGenerator : IWalveroIdGenerator
    {
        private readonly ISequenceRepository _sequenceRepository;
        private readonly ILogger<WalveroIdGenerator>? _logger;

        public WalveroIdGenerator(
            ISequenceRepository sequenceRepository,
            ILogger<WalveroIdGenerator>? logger = null)
        {
            _sequenceRepository = sequenceRepository ?? throw new ArgumentNullException(nameof(sequenceRepository));
            _logger = logger;
        }

        public async Task<long> GenerateWalveroIdAsync(int countryCode, CancellationToken ct = default)
        {
            if (countryCode <= 0)
                throw new ArgumentException("CountryCode must be greater than 0", nameof(countryCode));

            try
            {
                // Get next sequence atomically
                var lastNumber = await _sequenceRepository.GetNextCountrySequenceAsync(countryCode, ct);

                // Format: {countryCode}1{seq:D5}
                // Example: 994100001, 994100002, 336100001, ...
                var walveroId = long.Parse($"{countryCode}1{lastNumber:D5}");

                _logger?.LogDebug("Generated WalveroId for CountryCode {CountryCode}: {WalveroId}",
                    countryCode, walveroId);

                return walveroId;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating WalveroId for CountryCode {CountryCode}", countryCode);
                throw;
            }
        }

        public int? ExtractCountryCode(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return null;

            // Remove all non-digit characters except +
            var cleaned = phoneNumber.Trim();

            // Patterns:
            // +994501234567 -> 994
            // 994501234567 -> 994
            // 0501234567 (Azerbaijan) -> 994
            // 336123456789 (France) -> 336

            // Try +countryCode pattern first
            if (cleaned.StartsWith("+"))
            {
                // Extract first 1-3 digits after +
                var match = Regex.Match(cleaned.Substring(1), @"^(\d{1,3})");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var code))
                {
                    return code;
                }
            }

            // Try countryCode pattern (2-3 digits at start)
            var countryMatch = Regex.Match(cleaned, @"^(\d{2,3})");
            if (countryMatch.Success && int.TryParse(countryMatch.Groups[1].Value, out var countryCode))
            {
                // Common country codes
                if (countryCode >= 1 && countryCode <= 999)
                {
                    // Special case: Azerbaijan local numbers starting with 0
                    if (cleaned.StartsWith("0") && cleaned.Length >= 9)
                    {
                        return 994; // Azerbaijan country code
                    }

                    return countryCode;
                }
            }

            _logger?.LogWarning("Could not extract country code from phone number: {Phone}", 
                phoneNumber?.Substring(0, Math.Min(10, phoneNumber.Length)));

            return null;
        }
    }
}

