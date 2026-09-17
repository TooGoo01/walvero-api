using Loyalty.Business.Models.TenantIntegration;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.Infrastructure.Utilities.Security.Encryption;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class TenantIntegrationResolver : ITenantIntegrationResolver
    {
        private readonly ITenantIntegrationRepository _tenantIntegrationRepository;
        private readonly ITenantIntegrationVenueRepository _venueRepository;
        private readonly ISensitiveDataProtector _protector;

        public TenantIntegrationResolver(
            ITenantIntegrationRepository tenantIntegrationRepository,
            ITenantIntegrationVenueRepository venueRepository,
            ISensitiveDataProtector protector)
        {
            _tenantIntegrationRepository = tenantIntegrationRepository;
            _venueRepository = venueRepository;
            _protector = protector;
        }

        public async Task<WalletWebhookResolvedContext?> ResolveByWalletTokenAsync(string bearerToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(bearerToken))
                return null;

            var hash = _protector.ComputeTokenHash(bearerToken);
            var ti = await _tenantIntegrationRepository.GetByWalletTokenHashAsync(hash, ct);
            return MapToContext(ti);
        }

        public async Task<WalletWebhookResolvedContext?> ResolveByVenueAndSourceAsync(string venueId, string source, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(venueId) || string.IsNullOrWhiteSpace(source))
                return null;

            // Per-branch: əvvəl venue-səviyyə organizationId axtar → BranchId ilə context.
            var venue = await _venueRepository.GetByExternalVenueIdAndSourceAsync(venueId, source, ct);
            if (venue != null)
                return MapVenueToContext(venue);

            // Backward-compat: legacy tək-org tenant (organizationId parent-də).
            var ti = await _tenantIntegrationRepository.GetByVenueAndSourceAsync(venueId, source, ct);
            return MapToContext(ti);
        }

        public async Task<WalletWebhookResolvedContext?> ResolveByTerminalGroupIdAsync(string terminalGroupId, string source, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(terminalGroupId) || string.IsNullOrWhiteSpace(source))
                return null;

            // Per-branch: əvvəl venue-səviyyə terminalGroupId axtar → BranchId + org id ilə context.
            var venue = await _venueRepository.GetByTerminalGroupIdAndSourceAsync(terminalGroupId, source, ct);
            if (venue != null)
                return MapVenueToContext(venue);

            // Backward-compat: legacy tək-org tenant (terminalGroupId parent-də).
            var ti = await _tenantIntegrationRepository.GetByTerminalGroupIdAndSourceAsync(terminalGroupId, source, ct);
            return MapToContext(ti);
        }

        public Task<TenantIntegration?> GetByIdAsync(int tenantIntegrationId, CancellationToken ct = default)
        {
            return _tenantIntegrationRepository.GetByIdAsync(tenantIntegrationId, ct);
        }

        private static WalletWebhookResolvedContext? MapToContext(TenantIntegration? ti)
        {
            if (ti == null)
                return null;

            return new WalletWebhookResolvedContext
            {
                TenantId = ti.TenantId,
                LoyaltyProgramId = ti.LoyaltyProgramId,
                Source = ti.IntegrationType?.Source ?? string.Empty,
                TenantIntegrationId = ti.Id,
                ExternalVenueId = ti.ExternalVenueId
            };
        }

        private static WalletWebhookResolvedContext MapVenueToContext(TenantIntegrationVenue venue)
        {
            var ti = venue.TenantIntegration;
            return new WalletWebhookResolvedContext
            {
                TenantId = ti.TenantId,
                LoyaltyProgramId = ti.LoyaltyProgramId,
                Source = ti.IntegrationType?.Source ?? string.Empty,
                TenantIntegrationId = ti.Id,
                ExternalVenueId = venue.ExternalVenueId,
                BranchId = venue.BranchId,
                TenantIntegrationVenueId = venue.Id
            };
        }
    }
}
