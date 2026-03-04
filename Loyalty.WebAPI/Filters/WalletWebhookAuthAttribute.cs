using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Filters
{
    /// <summary>
    /// Apply to wallet webhook endpoints. Validates Bearer wallet_token and sets WalletWebhookContext.
    /// </summary>
    public class WalletWebhookAuthAttribute : ServiceFilterAttribute
    {
        public WalletWebhookAuthAttribute() : base(typeof(WalletWebhookAuthFilter))
        {
        }
    }
}
