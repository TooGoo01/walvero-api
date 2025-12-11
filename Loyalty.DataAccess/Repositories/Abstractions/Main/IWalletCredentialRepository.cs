using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IWalletCredentialRepository : IGenericRepository<WalletCredential>
    {
        Task<WalletCredential?> GetActiveAsync(int tenantId, WalletPlatform platform, CancellationToken ct = default);
        Task<List<WalletCredential>> ListAsync(int tenantId, WalletPlatform? platform = null, CancellationToken ct = default);

        /// <summary>Tenant + platform üzrə yalnız bir credential aktiv qalsın.</summary>
        Task SetActiveAsync(int tenantId, long credentialId, CancellationToken ct = default);

        Task DeactivateAsync(int tenantId, long credentialId, CancellationToken ct = default);
        Task<bool> ExistsAsync(int tenantId, WalletPlatform platform, CancellationToken ct = default);

        /// <summary>İmza/issuance üçün ehtiyac olan sahələri gətir (NoTracking).</summary>
        Task<WalletCredential?> FindForSigningAsync(int tenantId, WalletPlatform platform, CancellationToken ct = default);
    }
}
