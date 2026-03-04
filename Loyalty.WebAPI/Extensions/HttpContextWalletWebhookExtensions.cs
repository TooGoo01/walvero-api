using Loyalty.Business.Models.TenantIntegration;
using Loyalty.WebAPI.Filters;

namespace Loyalty.WebAPI.Extensions
{
    public static class HttpContextWalletWebhookExtensions
    {
        public static WalletWebhookResolvedContext? GetWalletWebhookContext(this HttpContext httpContext)
        {
            if (httpContext.Items.TryGetValue(WalletWebhookAuthFilter.WalletWebhookContextKey, out var obj) &&
                obj is WalletWebhookResolvedContext ctx)
            {
                return ctx;
            }
            return null;
        }
    }
}
