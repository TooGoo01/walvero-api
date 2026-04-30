using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// iiko POS-dan gələn webhook-ları qəbul edir.
    /// Auth header tələb etmir — tenant payload-dakı organizationId-dən resolve olunur.
    /// </summary>
    [ApiController]
    [Route("api/wallet")]
    [AllowAnonymous]
    public class IikoWebhookController : ControllerBase
    {
        private const string IikoSource = "iiko_pos";

        private readonly ITenantIntegrationResolver _resolver;
        private readonly IWalletWebhookService _webhookService;
        private readonly ILogger<IikoWebhookController> _logger;

        public IikoWebhookController(
            ITenantIntegrationResolver resolver,
            IWalletWebhookService webhookService,
            ILogger<IikoWebhookController> logger)
        {
            _resolver = resolver;
            _webhookService = webhookService;
            _logger = logger;
        }

        [HttpPost("iiko_webhook")]
        public async Task<IActionResult> Receive([FromBody] IikoWebhookRequest request, CancellationToken ct)
        {
            try
            {
                // Tam JSON logla — Azure Log Stream / App Insights-da görünəcək
                _logger.LogInformation(
                    "iiko webhook RAW: {RawJson}",
                    JsonSerializer.Serialize(request));

                _logger.LogInformation(
                    "iiko webhook received: orgId={OrgId} txId={TxId} type={Type} sum={Sum} customerId={CustomerId}",
                    request?.OrganizationId, request?.TransactionId, request?.TransactionType, request?.Sum, request?.CustomerId);

                if (request == null || string.IsNullOrWhiteSpace(request.OrganizationId))
                    return Ok(new { success = false, message = "organizationId is required" });

                var ctx = await _resolver.ResolveByVenueAndSourceAsync(request.OrganizationId, IikoSource, ct);
                if (ctx == null)
                {
                    _logger.LogWarning("iiko webhook: tenant not found for organizationId={OrgId}", request.OrganizationId);
                    return Ok(new { success = false, message = "Tenant integration not found for organizationId" });
                }

                var result = await _webhookService.ProcessIikoTransactionAsync(ctx, new IikoTransactionRequest
                {
                    Sum = request.Sum ?? 0,
                    Balance = request.Balance ?? 0,
                    OrderId = request.OrderId,
                    OrderNumber = request.OrderNumber,
                    IsDelivery = request.IsDelivery,
                    TerminalGroupId = request.TerminalGroupId,
                    WalletId = request.WalletId,
                    Id = request.Id,
                    OrganizationId = request.OrganizationId,
                    UocId = request.UocId,
                    NotificationType = request.NotificationType,
                    CustomerId = request.CustomerId ?? "",
                    Phone = request.Phone,
                    TransactionType = request.TransactionType ?? "",
                    TransactionId = request.TransactionId ?? "",
                    SubscriptionPassword = request.SubscriptionPassword,
                    ChangedOn = request.ChangedOn
                }, ct);

                return Ok(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                // Webhook best practice: 200 qaytar ki, iiko sonsuz retry etməsin.
                _logger.LogError(ex, "iiko webhook unhandled error");
                return Ok(new { success = false, message = "Internal error" });
            }
        }
    }
}
