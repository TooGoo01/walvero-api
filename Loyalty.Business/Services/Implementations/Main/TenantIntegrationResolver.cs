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
        private readonly ISensitiveDataProtector _protector;

        public TenantIntegrationResolver(
            ITenantIntegrationRepository tenantIntegrationRepository,
            ISensitiveDataProtector protector)
        {
            _tenantIntegrationRepository = tenantIntegrationRepository;
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

            var ti = await _tenantIntegrationRepository.GetByVenueAndSourceAsync(venueId, source, ct);
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
    }
}
