using Loyalty.Business.Services.Abstractions.Sequences;
using Loyalty.DataAccess.Repositories.Abstractions.Sequences;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Sequences
{
    /// <summary>
    /// Service for generating unique CardNumbers using atomic sequence generation
    /// Format: {prefix}{tenantId:D2}{prefix}{seq:D4}
    /// </summary>
    public class CardNumberGenerator : ICardNumberGenerator
    {
        private readonly ISequenceRepository _sequenceRepository;
        private readonly ILogger<CardNumberGenerator>? _logger;

        public CardNumberGenerator(
            ISequenceRepository sequenceRepository,
            ILogger<CardNumberGenerator>? logger = null)
        {
            _sequenceRepository = sequenceRepository ?? throw new ArgumentNullException(nameof(sequenceRepository));
            _logger = logger;
        }

        public async Task<string> GenerateCardNumberAsync(int tenantId, CancellationToken ct = default)
        {
            if (tenantId <= 0)
                throw new ArgumentException("TenantId must be greater than 0", nameof(tenantId));

            try
            {
                // Get next sequence atomically
                var (lastNumber, prefix) = await _sequenceRepository.GetNextTenantSequenceAsync(tenantId, ct);

                // Format: {prefix}{tenantId:D2}{prefix}{seq:D4}
                // Example: 789017890001, 789017890002, ...
                var cardNumber = $"{prefix}{tenantId:D2}{prefix}{lastNumber:D4}";

                _logger?.LogDebug("Generated CardNumber for TenantId {TenantId}: {CardNumber}",
                    tenantId, cardNumber);

                return cardNumber;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating CardNumber for TenantId {TenantId}", tenantId);
                throw;
            }
        }
    }
}

