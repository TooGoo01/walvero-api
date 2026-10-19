using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Tenancy
{
    public static class TenantScopeExtensions
    {
        /// <summary>
        /// ?tenantId= sorğu parametri ilə tenant: tenant-a bağlı işçi (tenant/filial admini, moderator) öz tenant-ından
        /// kənara çıxa bilməz; Super Admin istənilən tenant-ı seçir. Bax <see cref="TenantScopeRules"/>.
        /// </summary>
        public static int? ScopedTenantId(this ControllerBase controller, int? queryTenantId, ITenantProvider tenantProvider)
        {
            var user = controller.User;
            return TenantScopeRules.ResolveQueryOverride(
                isSuperAdmin: user.IsInRole("Super Admin"),
                isTenantBoundStaff: TenantScopeRules.TenantBoundRoles.Any(user.IsInRole),
                queryTenantId: queryTenantId,
                currentTenantId: tenantProvider.CurrentTenantId);
        }
    }
}
