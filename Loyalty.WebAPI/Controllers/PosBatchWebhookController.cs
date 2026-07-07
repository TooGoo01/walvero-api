using Hangfire;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Implementations.Main;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// iiko POS-dan gələn OrderClosed / OrderReturned event-ləri qəbul edir.
    /// Auth header tələb etmir — tenant payload-dakı Restaurant.DepartmentId-dən resolve olunur.
    /// Event Hangfire-ə enqueue olunur, HTTP cavab dərhal qayıdır.
    /// </summary>
    [ApiController]
    [Route("api/wallet")]
    [AllowAnonymous]
    public class PosBatchWebhookController : ControllerBase
    {
        private const string IikoSource = "iiko_pos";

        private readonly ITenantIntegrationResolver _resolver;
        private readonly IBackgroundJobClient _jobClient;
        private readonly ITenantStatusGuard _tenantStatusGuard;
        private readonly ILogger<PosBatchWebhookController> _logger;

        public PosBatchWebhookController(
            ITenantIntegrationResolver resolver,
            IBackgroundJobClient jobClient,
            ITenantStatusGuard tenantStatusGuard,
            ILogger<PosBatchWebhookController> logger)
        {
            _resolver = resolver;
            _jobClient = jobClient;
            _tenantStatusGuard = tenantStatusGuard;
            _logger = logger;
        }

        [HttpPost("pos_batch_webhook")]
        public async Task<IActionResult> Receive([FromBody] IikoOrderEventWebhookPayload payload, CancellationToken ct)
        {
            var traceId = HttpContext.TraceIdentifier;
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var sw = Stopwatch.StartNew();

            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = traceId,
                ["ClientIp"] = clientIp,
                ["Endpoint"] = "pos_batch_webhook"
            });

            try
            {
                _logger.LogInformation(
                    "iiko order event RECEIVED. traceId={TraceId} clientIp={ClientIp} eventType={EventType} orderId={OrderId} orderNumber={OrderNumber} totalSum={TotalSum} fullSum={FullSum} deptId={DeptId} customerCard={CardNumber}",
                    traceId, clientIp,
                    payload?.EventType, payload?.OrderId, payload?.OrderNumber,
                    payload?.TotalSum, payload?.FullSum,
                    payload?.Restaurant?.DepartmentId,
                    payload?.Customer?.FirstOrDefault()?.CardNumber);

                _logger.LogDebug("iiko order event RAW: {RawJson}", JsonSerializer.Serialize(payload));

                if (payload == null)
                    return Ok(new { success = false, message = "payload required" });

                if (string.IsNullOrWhiteSpace(payload.OrderId))
                    return Ok(new { success = false, message = "OrderId is required" });

                if (string.IsNullOrWhiteSpace(payload.EventType))
                    return Ok(new { success = false, message = "EventType is required" });

                var venueId = payload.Restaurant?.DepartmentId;
                if (string.IsNullOrWhiteSpace(venueId))
                    return Ok(new { success = false, message = "Restaurant.DepartmentId is required" });

                var resolveSw = Stopwatch.StartNew();
                var ctx = await _resolver.ResolveByVenueAndSourceAsync(venueId, IikoSource, ct);
                resolveSw.Stop();

                if (ctx == null)
                {
                    _logger.LogWarning(
                        "iiko order event TENANT_NOT_FOUND. traceId={TraceId} departmentId={DeptId} resolveMs={ResolveMs}",
                        traceId, venueId, resolveSw.ElapsedMilliseconds);
                    return Ok(new { success = false, message = "Tenant integration not found" });
                }

                _logger.LogInformation(
                    "iiko order event TENANT_RESOLVED. traceId={TraceId} tenantId={TenantId} integrationId={IntegrationId} loyaltyProgramId={LoyaltyProgramId} resolveMs={ResolveMs}",
                    traceId, ctx.TenantId, ctx.TenantIntegrationId, ctx.LoyaltyProgramId, resolveSw.ElapsedMilliseconds);

                // Deaktiv tenant-da POS batch auto-earn qadağandır (abunə dayandırılıb)
                if (!await _tenantStatusGuard.IsActiveAsync(ctx.TenantId, ct))
                {
                    _logger.LogWarning("iiko order event blocked — tenant inactive. TenantId={TenantId}", ctx.TenantId);
                    return Ok(new { success = false, message = Business.Services.Implementations.Main.TenantStatusGuard.InactiveMessage });
                }

                var customer = payload.Customer?.FirstOrDefault();

                var mapped = new IikoOrderEventRequest
                {
                    EventType = payload.EventType,
                    OrderId = payload.OrderId,
                    OrderNumber = payload.OrderNumber,
                    TotalSum = payload.TotalSum,
                    FullSum = payload.FullSum,
                    DepartmentId = payload.Restaurant?.DepartmentId,
                    TerminalGroupId = payload.TerminalsGroup?.Id,
                    ExternalCustomerId = customer?.Id,
                    CustomerPhone = customer?.Phone,
                    CustomerCardNumber = customer?.CardNumber,
                    Items = payload.Items?.Select(i => new IikoOrderItem
                    {
                        ProductId = i.ProductId ?? string.Empty,
                        ProductName = i.ProductName,
                        ProductNumber = i.ProductNumber,
                        Price = i.Price,
                        Amount = i.Amount,
                        Sum = i.Sum
                    }).ToList() ?? new List<IikoOrderItem>(),
                    Discounts = payload.Discounts?.Select(d => new IikoOrderDiscount
                    {
                        DiscountTypeId = d.DiscountTypeId,
                        DiscountTypeName = d.DiscountTypeName,
                        DiscountSum = d.DiscountSum,
                        DiscountByItem = d.DiscountByItem?.Select(dbi => new IikoOrderDiscountByItem
                        {
                            OrderItemId = dbi.OrderItemId,
                            ProductId = dbi.ProductId,
                            ProductName = dbi.ProductName,
                            DiscountSum = dbi.DiscountSum
                        }).ToList() ?? new List<IikoOrderDiscountByItem>()
                    }).ToList() ?? new List<IikoOrderDiscount>(),
                    Payments = payload.Payments?.Select(p => new IikoOrderPayment
                    {
                        PaymentTypeId = p.PaymentTypeId,
                        PaymentTypeName = p.PaymentTypeName,
                        Sum = p.Sum,
                        Credential = p.AdditionalData?.Credential,
                        SearchScope = p.AdditionalData?.SearchScope
                    }).ToList() ?? new List<IikoOrderPayment>()
                };

                string jobId;
                try
                {
                    jobId = _jobClient.Enqueue<PosBatchOperationJob>(j => j.ProcessAsync(ctx, mapped, CancellationToken.None));
                }
                catch (Exception enqueueEx)
                {
                    _logger.LogError(enqueueEx,
                        "iiko order event ENQUEUE_FAIL. traceId={TraceId} orderId={OrderId}",
                        traceId, payload.OrderId);
                    return Ok(new { success = false, message = "Failed to enqueue" });
                }

                sw.Stop();

                _logger.LogInformation(
                    "iiko order event ENQUEUED. traceId={TraceId} jobId={JobId} eventType={EventType} orderId={OrderId} customerId={ExtCustId} phone={Phone} card={Card} itemCount={ItemCount} paymentCount={PayCount} walletCredentialPresent={HasCredential} resolveMs={ResolveMs} totalMs={TotalMs}",
                    traceId, jobId,
                    mapped.EventType, mapped.OrderId, mapped.ExternalCustomerId, mapped.CustomerPhone, mapped.CustomerCardNumber,
                    mapped.Items.Count, mapped.Payments.Count,
                    mapped.Payments.Any(p => !string.IsNullOrWhiteSpace(p.Credential)),
                    resolveSw.ElapsedMilliseconds, sw.ElapsedMilliseconds);

                return Ok(new
                {
                    success = true,
                    eventType = mapped.EventType,
                    orderId = mapped.OrderId,
                    jobId,
                    message = "Accepted for background processing"
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "iiko order event UNHANDLED_ERROR. traceId={TraceId} totalMs={TotalMs}",
                    traceId, sw.ElapsedMilliseconds);
                return Ok(new { success = false, message = "Internal error" });
            }
        }
    }

    public class IikoOrderEventWebhookPayload
    {
        public string EventType { get; set; } = string.Empty;
        public string? Timestamp { get; set; }
        public IikoEventRestaurant? Restaurant { get; set; }
        public IikoEventTerminalsGroup? TerminalsGroup { get; set; }
        public IikoEventTerminal? Terminal { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public int? OrderNumber { get; set; }
        public decimal TotalSum { get; set; }
        public decimal FullSum { get; set; }
        public IikoEventTable? Table { get; set; }
        public IikoEventCashier? Cashier { get; set; }
        public List<IikoEventItem> Items { get; set; } = new();
        public List<IikoEventDiscount> Discounts { get; set; } = new();
        public List<IikoEventCustomer> Customer { get; set; } = new();
        public List<IikoEventPayment> Payments { get; set; } = new();
    }

    public class IikoEventRestaurant
    {
        public string? Name { get; set; }
        public string? DepartmentId { get; set; }
    }

    public class IikoEventTerminalsGroup
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public class IikoEventTerminal
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public class IikoEventTable
    {
        public string? Id { get; set; }
        public int? Number { get; set; }
    }

    public class IikoEventCashier
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public class IikoEventItem
    {
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductNumber { get; set; }
        public string? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? SizeId { get; set; }
        public string? SizeName { get; set; }
        public decimal Price { get; set; }
        public decimal Amount { get; set; }
        public decimal Sum { get; set; }
        public List<IikoEventModifier> Modifiers { get; set; } = new();
    }

    public class IikoEventModifier
    {
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductNumber { get; set; }
        public decimal Price { get; set; }
        public decimal Amount { get; set; }
    }

    public class IikoEventDiscount
    {
        public string? DiscountTypeId { get; set; }
        public string? DiscountTypeName { get; set; }
        public decimal DiscountSum { get; set; }
        public List<IikoEventDiscountByItem> DiscountByItem { get; set; } = new();
    }

    public class IikoEventDiscountByItem
    {
        public string? OrderItemId { get; set; }
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal DiscountSum { get; set; }
    }

    public class IikoEventCustomer
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? CardNumber { get; set; }
        public string? Gender { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
    }

    public class IikoEventPayment
    {
        public string? PaymentTypeId { get; set; }
        public string? PaymentTypeName { get; set; }
        public decimal Sum { get; set; }
        public IikoEventPaymentAdditionalData? AdditionalData { get; set; }
    }

    public class IikoEventPaymentAdditionalData
    {
        public string? Credential { get; set; }
        public int? SearchScope { get; set; }
    }
}
