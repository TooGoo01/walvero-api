using Hangfire.Dashboard;

namespace Loyalty.WebAPI.Hangfire
{
    /// <summary>
    /// Hangfire dashboard yalnız Tenant Admin / Super Admin rolu üçün açıqdır.
    /// </summary>
    public sealed class TenantAdminDashboardFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var http = context.GetHttpContext();
            if (http.User?.Identity?.IsAuthenticated != true) return false;

            return http.User.IsInRole("Tenant Admin")
                || http.User.IsInRole("Super Admin")
                || http.User.IsInRole("TenantAdmin")
                || http.User.IsInRole("SuperAdmin");
        }
    }
}
