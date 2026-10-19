using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class IikoConnectionResolver : IIikoConnectionResolver
    {
        private const string IikoSource = "iiko_pos";

        private readonly LoyaltyWalletDbContext _db;
        private readonly IIikoPosApiClient _iiko;
        private readonly ISensitiveDataProtector _protector;
        private readonly ILogger<IikoConnectionResolver> _logger;

        public IikoConnectionResolver(LoyaltyWalletDbContext db, IIikoPosApiClient iiko, ISensitiveDataProtector protector, ILogger<IikoConnectionResolver> logger)
        {
            _db = db;
            _iiko = iiko;
            _protector = protector;
            _logger = logger;
        }

        public async Task<(TenantIntegration? Integration, string? Error)> GetOwnedIikoIntegrationAsync(int tenantId, int integrationId, CancellationToken ct = default)
        {
            var ti = await _db.TenantIntegrations.AsNoTracking()
                .Include(t => t.IntegrationType)
                .FirstOrDefaultAsync(t => t.Id == integrationId && t.TenantId == tenantId, ct);
            if (ti == null) return (null, "İnteqrasiya tapılmadı.");
            if (ti.IntegrationType?.Source != IikoSource) return (null, "Bu iiko inteqrasiyası deyil.");
            return (ti, null);
        }

        public async Task<List<IikoOrgTarget>> GetOrgsAsync(TenantIntegration integration, CancellationToken ct = default)
        {
            var targets = await _db.TenantIntegrationVenues.AsNoTracking()
                .Where(v => v.TenantIntegrationId == integration.Id && v.IsActive)
                .OrderBy(v => v.Id)
                .Select(v => new IikoOrgTarget
                {
                    VenueId = v.Id,
                    BranchId = v.BranchId,
                    BranchName = v.Branch != null ? v.Branch.Name : null,
                    DisplayName = v.DisplayName,
                    OrganizationId = v.ExternalVenueId
                })
                .ToListAsync(ct);
            if (targets.Count == 0 && !string.IsNullOrWhiteSpace(integration.ExternalVenueId))
                targets.Add(new IikoOrgTarget { OrganizationId = integration.ExternalVenueId, DisplayName = integration.DisplayName });
            return targets;
        }

        public async Task<(string? Token, string? Error)> GetTokenAsync(TenantIntegration integration, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(integration.ApiTokenEncrypted)) return (null, "apiLogin konfiqurasiya edilməyib.");
            var apiLogin = _protector.Decrypt(integration.ApiTokenEncrypted);
            if (string.IsNullOrEmpty(apiLogin)) return (null, "apiLogin oxunmadı.");
            var clientSecret = string.IsNullOrEmpty(integration.ApiSecretEncrypted) ? null : _protector.Decrypt(integration.ApiSecretEncrypted);
            try
            {
                var token = await _iiko.GetAccessTokenAsync(apiLogin, integration.ApiAppId, clientSecret, ct: ct);
                return string.IsNullOrEmpty(token) ? (null, "iiko access_token alınmadı (credential-ları yoxla).") : (token, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko token failed. IntegrationId={Id}", integration.Id);
                return (null, "iiko access_token alınmadı.");
            }
        }
    }
}
