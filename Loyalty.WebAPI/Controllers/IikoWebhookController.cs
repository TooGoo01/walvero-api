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
        private readonly ITenantStatusGuard _tenantStatusGuard;
        private readonly ILogger<IikoWebhookController> _logger;

        public IikoWebhookController(
            ITenantIntegrationResolver resolver,
            IWalletWebhookService webhookService,
            ITenantStatusGuard tenantStatusGuard,
            ILogger<IikoWebhookController> logger)
        {
            _resolver = resolver;
            _webhookService = webhookService;
            _tenantStatusGuard = tenantStatusGuard;
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
                if (ctx == null && !string.IsNullOrWhiteSpace(request.UocId))
                {
                    ctx = await _resolver.ResolveByVenueAndSourceAsync(request.UocId, IikoSource, ct);
                }
                if (ctx == null)
                {
                    _logger.LogWarning(
                        "iiko webhook: tenant not found. organizationId={OrgId} uocId={UocId}",
                        request.OrganizationId, request.UocId);
                    return Ok(new { success = false, message = "Tenant integration not found" });
                }

                // Deaktiv tenant-da POS auto-earn qadağandır (abunə dayandırılıb)
                if (!await _tenantStatusGuard.IsActiveAsync(ctx.TenantId, ct))
                {
                    _logger.LogWarning("iiko webhook blocked — tenant inactive. TenantId={TenantId}", ctx.TenantId);
                    return Ok(new { success = false, message = Business.Services.Implementations.Main.TenantStatusGuard.InactiveMessage });
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

                _logger.LogInformation(
                    "iiko webhook processed: txId={TxId} type={Type} sum={Sum} customerId={CustomerId} success={Success} message={Message}",
                    request.TransactionId, request.TransactionType, request.Sum, request.CustomerId, result.Success, result.Message);

                return Ok(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                // Webhook best practice: 200 qaytar ki, iiko sonsuz retry etməsin.
                _logger.LogError(ex, "iiko webhook unhandled error");
                return Ok(new { success = false, message = "Internal error" });
            }
        }

        /// <summary>
        /// iiko OrderClosed webhook — token tələb etmir.
        /// Tenant payload-dakı TerminalsGroup.Id-dən resolve olunur.
        /// </summary>
        [HttpPost("iiko_transaction")]
        public async Task<IActionResult> IikoTransaction([FromBody] IikoOrderEventWebhookRequest request, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation(
                    "iiko_transaction RAW: {RawJson}",
                    JsonSerializer.Serialize(request));

                var terminalGroupId = request?.TerminalsGroup?.Id;
                if (request == null || string.IsNullOrWhiteSpace(terminalGroupId))
                    return Ok(new { success = false, message = "TerminalsGroup.Id is required" });

                var ctx = await _resolver.ResolveByTerminalGroupIdAsync(terminalGroupId, IikoSource, ct);
                if (ctx == null)
                {
                    _logger.LogWarning(
                        "iiko_transaction: tenant not found. terminalGroupId={TerminalGroupId} orderId={OrderId}",
                        terminalGroupId, request.OrderId);
                    return Ok(new { success = false, message = "Tenant integration not found" });
                }

                // Deaktiv tenant-da POS auto-earn qadağandır (abunə dayandırılıb)
                if (!await _tenantStatusGuard.IsActiveAsync(ctx.TenantId, ct))
                {
                    _logger.LogWarning("iiko_transaction blocked — tenant inactive. TenantId={TenantId}", ctx.TenantId);
                    return Ok(new { success = false, message = Business.Services.Implementations.Main.TenantStatusGuard.InactiveMessage });
                }

                var firstCustomer = request.Customer?.FirstOrDefault();

                var internalReq = new IikoOrderEventRequest
                {
                    EventType = string.IsNullOrWhiteSpace(request.EventType) ? "OrderClosed" : request.EventType!,
                    OrderId = request.OrderId ?? string.Empty,
                    OrderNumber = request.OrderNumber,
                    TotalSum = request.TotalSum ?? 0m,
                    FullSum = request.FullSum ?? 0m,
                    DepartmentId = request.Restaurant?.DepartmentId,
                    TerminalGroupId = request.TerminalsGroup?.Id,
                    ExternalCustomerId = firstCustomer?.Id,
                    CustomerPhone = firstCustomer?.Phone,
                    CustomerCardNumber = firstCustomer?.CardNumber,
                    Items = (request.Items ?? new()).Select(i => new IikoOrderItem
                    {
                        ProductId = i.ProductId ?? string.Empty,
                        ProductName = i.ProductName,
                        ProductNumber = i.ProductNumber,
                        Price = i.Price,
                        Amount = i.Amount,
                        Sum = i.Sum
                    }).ToList(),
                    Discounts = (request.Discounts ?? new()).Select(d => new IikoOrderDiscount
                    {
                        DiscountTypeId = d.DiscountTypeId,
                        DiscountTypeName = d.DiscountTypeName,
                        DiscountSum = d.DiscountSum
                    }).ToList(),
                    Payments = (request.Payments ?? new()).Select(p => new IikoOrderPayment
                    {
                        PaymentTypeId = p.PaymentTypeId,
                        PaymentTypeName = p.PaymentTypeName,
                        Sum = p.Sum,
                        Credential = p.AdditionalData?.Credential,
                        SearchScope = p.AdditionalData?.SearchScope
                    }).ToList()
                };

                var result = await _webhookService.ProcessIikoOrderEventAsync(ctx, internalReq, ct);

                _logger.LogInformation(
                    "iiko_transaction processed: orderId={OrderId} terminalGroupId={TerminalGroupId} success={Success} message={Message}",
                    request.OrderId, terminalGroupId, result.Success, result.Message);

                return Ok(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                // Webhook best practice: 200 qaytar ki, iiko sonsuz retry etməsin.
                _logger.LogError(ex, "iiko_transaction unhandled error");
                return Ok(new { success = false, message = "Internal error" });
            }
        }
    }
}
