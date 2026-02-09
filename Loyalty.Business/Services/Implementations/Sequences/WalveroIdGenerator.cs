using Loyalty.Business.Services.Abstractions.Sequences;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Repositories.Abstractions.Sequences;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Sequences
{
    /// <summary>
    /// Service for generating unique WalveroId using atomic sequence generation
    /// Format: {countryCode}1{seq:D5}
    /// </summary>
    public class WalveroIdGenerator : IWalveroIdGenerator
    {
        private readonly ISequenceRepository _sequenceRepository;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly ILogger<WalveroIdGenerator>? _logger;

        public WalveroIdGenerator(
            ISequenceRepository sequenceRepository,
            LoyaltyWalletDbContext dbContext,
            ILogger<WalveroIdGenerator>? logger = null)
        {
            _sequenceRepository = sequenceRepository ?? throw new ArgumentNullException(nameof(sequenceRepository));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
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

        public async Task<int?> ExtractCountryCodeAsync(string? phoneNumber, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return null;

            try
            {
                // Remove all non-digit characters except +
                var cleaned = phoneNumber.Trim();

                // Patterns:
                // +994501234567 -> 994
                // 994501234567 -> 994
                // +447911123456 (UK) -> 44
                // 447911123456 (UK) -> 44
                // +12025551234 (USA) -> 1
                // 12025551234 (USA) -> 1
                // 0501234567 (Azerbaijan) -> 994
                // 336123456789 (France) -> 336

                int? extractedCode = null;

                // Try +countryCode pattern first
                if (cleaned.StartsWith("+"))
                {
                    // Try 1-3 digits after + (check 1-digit codes first for better performance)
                    // USA: +1, UK: +44, Azerbaijan: +994, France: +33, etc.
                    // Start with 1-digit codes first (USA - 1) for better performance
                    for (int len = 1; len <= 3; len++)
                    {
                        if (cleaned.Length > len)
                        {
                            var potentialCode = cleaned.Substring(1, len);
                            if (int.TryParse(potentialCode, out var code) && code > 0)
                            {
                                // Check if this code exists in database
                                var exists = await _dbContext.CountrySequences
                                    .AnyAsync(cs => cs.CountryCode == code && cs.IsActive, ct);
                                
                                if (exists)
                                {
                                    extractedCode = code;
                                    _logger?.LogDebug("Extracted country code {CountryCode} from phone number (with + prefix)", code);
                                    break;
                                }
                            }
                        }
                    }
                }

                // If not found with + prefix, try without +
                if (!extractedCode.HasValue)
                {
                    // Try 1-3 digits at start (check 1-digit codes first for better performance)
                    for (int len = 1; len <= 3; len++)
                    {
                        if (cleaned.Length >= len)
                        {
                            var potentialCode = cleaned.Substring(0, len);
                            if (int.TryParse(potentialCode, out var code) && code > 0)
                            {
                                // Check if this code exists in database
                                var exists = await _dbContext.CountrySequences
                                    .AnyAsync(cs => cs.CountryCode == code && cs.IsActive, ct);
                                
                                if (exists)
                                {
                                    extractedCode = code;
                                    _logger?.LogDebug("Extracted country code {CountryCode} from phone number (without + prefix)", code);
                                    break;
                                }
                            }
                        }
                    }
                }

                // Special case: Azerbaijan local numbers starting with 0
                if (!extractedCode.HasValue && cleaned.StartsWith("0") && cleaned.Length >= 9)
                {
                    var exists = await _dbContext.CountrySequences
                        .AnyAsync(cs => cs.CountryCode == 994 && cs.IsActive, ct);
                    
                    if (exists)
                    {
                        extractedCode = 994; // Azerbaijan country code
                        _logger?.LogDebug("Extracted Azerbaijan country code (994) from local number starting with 0");
                    }
                }

                if (!extractedCode.HasValue)
                {
                    _logger?.LogWarning("Could not extract valid country code from phone number: {Phone}", 
                        phoneNumber?.Substring(0, Math.Min(10, phoneNumber.Length)));
                }

                return extractedCode;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error extracting country code from phone number: {Phone}", 
                    phoneNumber?.Substring(0, Math.Min(10, phoneNumber.Length)));
                return null;
            }
        }
    }
}

