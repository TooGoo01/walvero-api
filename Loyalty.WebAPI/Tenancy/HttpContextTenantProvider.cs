using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using System.Security.Claims;

namespace Loyalty.WebAPI.Tenancy
{
    public class HttpContextTenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpContextTenantProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Tenant-a bağlı işçi rolları üçün JWT tenant_id qalibdir (header spoofing ilə başqa tenant-a keçid yoxdur);
        // Super Admin header ilə tenant seçir; müştəri/anonim üçün bugünkü sıra (header → claim → cookie).
        public int? CurrentTenantId => TenantScopeRules.Resolve(
            isSuperAdmin: User?.IsInRole("Super Admin") == true,
            isTenantBoundStaff: TenantScopeRules.TenantBoundRoles.Any(r => User?.IsInRole(r) == true),
            claimTenantId: GetIntClaim("tenant_id"),
            headerTenantId: GetIntHeader("X-Tenant-Id"),
            cookieTenantId: GetIntClaimOrCookie("tenant_id", "TenantId"));
        public int? CurrentProgramId => GetIntHeader("X-Program-Id") ?? GetIntClaim("program_id");
        public int? CurrentBranchId => GetIntClaim("branch_id") ?? GetIntHeader("X-Branch-Id");
        public string? CurrentUserId => GetClaim(ClaimTypes.NameIdentifier);

        public string CurrentLanguage
        {
            get
            {
                var header = _httpContextAccessor.HttpContext?.Request.Headers.AcceptLanguage.ToString();
                if (string.IsNullOrWhiteSpace(header)) return "az";
                var lang = header.Split(',')[0].Trim();
                if (lang.Length >= 2) lang = lang.Substring(0, 2).ToLowerInvariant();
                return lang is "az" or "en" or "ru" ? lang : "az";
            }
        }

        private ClaimsPrincipal? User =>
            _httpContextAccessor.HttpContext?.User;

        private string? GetClaim(string claimType)
        {
            return User?.FindFirst(claimType)?.Value;
        }

        private int? GetIntClaim(string claimType)
        {
            var value = GetClaim(claimType);
            if (int.TryParse(value, out var id))
                return id;

            return null;
        }

        private int? GetIntHeader(string headerName)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Request.Headers.TryGetValue(headerName, out var headerVal) == true &&
                int.TryParse(headerVal, out var id))
            {
                return id;
            }
            return null;
        }

        private int? GetIntClaimOrCookie(string claimType, string cookieName)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            // 1️⃣ Əvvəl JWT claim-dən oxu
            var fromClaim = GetIntClaim(claimType);
            if (fromClaim.HasValue)
                return fromClaim;

            // 2️⃣ Tapılmadısa – cookie-dən oxu (EnsureTenantCookieFilter ilə uyğun)
            if (httpContext?.Request.Cookies.TryGetValue(cookieName, out var cookieVal) == true &&
                int.TryParse(cookieVal, out var id))
            {
                return id;
            }

            // 3️⃣ Heç yerdə yoxdursa – null
            return null;
        }
    }
}
