using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class WalletCredentialRepository : GenericRepository<WalletCredential>, IWalletCredentialRepository
    {
        public WalletCredentialRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }

        public Task<WalletCredential?> GetActiveAsync(int tenantId, WalletPlatform platform, CancellationToken ct = default)
           => GetAsQueryable().AsNoTracking()
                 .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Platform == platform && x.IsActive, ct);

        public async Task<List<WalletCredential>> ListAsync(int tenantId, WalletPlatform? platform = null, CancellationToken ct = default)
        {
            var q = GetAsQueryable().AsNoTracking().Where(x => x.TenantId == tenantId);
            if (platform.HasValue) q = q.Where(x => x.Platform == platform);
            return await q.OrderByDescending(x => x.IsActive).ThenBy(x => x.Id).ToListAsync(ct);
        }

        public async Task SetActiveAsync(int tenantId, long credentialId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public async Task DeactivateAsync(int tenantId, long credentialId, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
        public Task<bool> ExistsAsync(int tenantId, WalletPlatform platform, CancellationToken ct = default)
            => GetAsQueryable().AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.Platform == platform, ct);

        public Task<WalletCredential?> FindForSigningAsync(int tenantId, WalletPlatform platform, CancellationToken ct = default)
            => GetAsQueryable().AsNoTracking()
                  .Where(x => x.TenantId == tenantId && x.Platform == platform && x.IsActive)
                  .Select(x => new WalletCredential
                  {
                      Id = x.Id,
                      TenantId = x.TenantId,
                      Platform = x.Platform,
                      // Apple
                      TeamIdentifier = x.TeamIdentifier,
                      PassTypeIdentifier = x.PassTypeIdentifier,
                      AppleCertificateUrl = x.AppleCertificateUrl,
                      CertificatePassword = x.CertificatePassword,
                      // Google
                      IssuerId = x.IssuerId,
                      ServiceAccountEmail = x.ServiceAccountEmail,
                      GooglePrivateKeyUrl = x.GooglePrivateKeyUrl,
                      IsActive = x.IsActive
                  })
                  .FirstOrDefaultAsync(ct);
    }
}

        

