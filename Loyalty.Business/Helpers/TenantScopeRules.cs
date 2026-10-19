namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Cari tenant-ın təyini (DB-siz, test olunur). Tenant-a bağlı işçi rolları (tenant/filial admini, moderator,
    /// menyu admini) üçün JWT-dəki tenant_id həmişə qalibdir — X-Tenant-Id header-i və ?tenantId= sorğusu ilə
    /// başqa tenant-ın kontekstinə keçmək olmaz. Super Admin header ilə istənilən tenant-ı seçir; müştəri/guest
    /// və anonim (app, public səhifələr) üçün bugünkü davranış (header → claim → cookie).
    /// </summary>
    public static class TenantScopeRules
    {
        public static readonly string[] TenantBoundRoles =
        {
            "Tenant Admin", "Branch Admin", "Moderator", "Branch Moderator", "Menu Admin"
        };

        public static int? Resolve(bool isSuperAdmin, bool isTenantBoundStaff, int? claimTenantId, int? headerTenantId, int? cookieTenantId)
        {
            if (!isSuperAdmin && isTenantBoundStaff && claimTenantId.HasValue)
                return claimTenantId;
            return headerTenantId ?? claimTenantId ?? cookieTenantId;
        }

        /// <summary>
        /// Controller-lərdəki ?tenantId= sorğu parametri: tenant-a bağlı işçi üçün ignor edilir (öz tenant-ı),
        /// Super Admin və digərləri (müştəri/app) üçün bugünkü kimi sorğu → cari tenant.
        /// </summary>
        public static int? ResolveQueryOverride(bool isSuperAdmin, bool isTenantBoundStaff, int? queryTenantId, int? currentTenantId)
            => !isSuperAdmin && isTenantBoundStaff && currentTenantId.HasValue
                ? currentTenantId
                : queryTenantId ?? currentTenantId;
    }
}
