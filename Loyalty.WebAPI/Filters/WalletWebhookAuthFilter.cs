using Loyalty.Business.Models.TenantIntegration;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Implementations.Main;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Loyalty.WebAPI.Filters
{
    /// <summary>
    /// Validates Bearer wallet_token for POS webhook endpoints.
    /// Sets WalletWebhookContext in HttpContext.Items for downstream use.
    /// Returns 401 if token is missing or invalid.
    /// </summary>
    public class WalletWebhookAuthFilter : IAsyncActionFilter
    {
        public const string WalletWebhookContextKey = "WalletWebhookContext";
        private readonly ITenantIntegrationResolver _resolver;
        private readonly ITenantStatusGuard _tenantStatusGuard;

        public WalletWebhookAuthFilter(ITenantIntegrationResolver resolver, ITenantStatusGuard tenantStatusGuard)
        {
            _resolver = resolver;
            _tenantStatusGuard = tenantStatusGuard;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var authHeader = context.HttpContext.Request.Headers.Authorization.FirstOrDefault();
            string? token = null;

            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader["Bearer ".Length..].Trim();
            }

            if (string.IsNullOrEmpty(token))
            {
                context.Result = new UnauthorizedObjectResult(new { success = false, message = "Missing or invalid Authorization header" });
                return;
            }

            var resolved = await _resolver.ResolveByWalletTokenAsync(token, context.HttpContext.RequestAborted);
            if (resolved == null)
            {
                context.Result = new UnauthorizedObjectResult(new { success = false, message = "Invalid wallet token" });
                return;
            }

            // Deaktiv tenant-da heç bir webhook əməliyyatı getməsin (abunə dayandırılıb)
            if (!await _tenantStatusGuard.IsActiveAsync(resolved.TenantId, context.HttpContext.RequestAborted))
            {
                context.Result = new ObjectResult(new { success = false, message = TenantStatusGuard.InactiveMessage })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            context.HttpContext.Items[WalletWebhookContextKey] = resolved;
            await next();
        }
    }
}
