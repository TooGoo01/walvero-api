using Hangfire;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Implementations.Main;
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
        private readonly IIikoWebhookFailureService _failureService;
        private readonly IBackgroundJobClient _jobClient;
        private readonly ILogger<IikoWebhookController> _logger;

        public IikoWebhookController(
            ITenantIntegrationResolver resolver,
            IWalletWebhookService webhookService,
            ITenantStatusGuard tenantStatusGuard,
            IIikoWebhookFailureService failureService,
            IBackgroundJobClient jobClient,
            ILogger<IikoWebhookController> logger)
        {
            _resolver = resolver;
            _webhookService = webhookService;
            _tenantStatusGuard = tenantStatusGuard;
            _failureService = failureService;
            _jobClient = jobClient;
            _logger = logger;
        }

        private static readonly JsonSerializerOptions CaseInsensitiveJson = new()
        {
            PropertyNameCaseInsensitive = true
        };

        [HttpPost("iiko_webhook")]
        public async Task<IActionResult> Receive([FromBody] JsonElement rawBody, CancellationToken ct)
        {
            try
            {
                // ƏSL gələn JSON — format bizim DTO ilə uyğun gəlməyəndə bunu görmək lazımdır
                _logger.LogInformation("iiko webhook RAW BODY: {RawBody}", rawBody.GetRawText());

                // iiko Cloud webhook massiv göndərə bilər — ilk elementi götür
                var element = rawBody.ValueKind == JsonValueKind.Array && rawBody.GetArrayLength() > 0
                    ? rawBody[0]
                    : rawBody;

                IikoWebhookRequest? request = null;
                try
                {
                    request = JsonSerializer.Deserialize<IikoWebhookRequest>(element.GetRawText(), CaseInsensitiveJson);
                }
                catch (Exception dex)
                {
                    _logger.LogWarning(dex, "iiko webhook: payload DTO-ya deserialize olunmadı");
                }

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
        /// iiko OrderClosed / OrderReturned webhook — token tələb etmir.
        /// Tenant payload-dakı TerminalsGroup.Id-dən resolve olunur.
        /// Emal Hangfire-ə enqueue olunur, HTTP cavab dərhal qayıdır
        /// (iiko plugin ~5s timeout → sinxron emalda 499 + itirilmiş sifariş).
        /// </summary>
        [HttpPost("iiko_transaction")]
        public async Task<IActionResult> IikoTransaction([FromBody] JsonElement rawBody, CancellationToken ct)
        {
            IikoOrderEventRequest? internalReq = null;
            try
            {
                // ƏSL gələn JSON — format DTO ilə uyğun gəlməyəndə bunu görmək lazımdır
                _logger.LogInformation("iiko_transaction RAW BODY: {RawBody}", rawBody.GetRawText());

                var element = rawBody.ValueKind == JsonValueKind.Array && rawBody.GetArrayLength() > 0
                    ? rawBody[0]
                    : rawBody;

                IikoOrderEventWebhookRequest? request = null;
                try
                {
                    request = JsonSerializer.Deserialize<IikoOrderEventWebhookRequest>(element.GetRawText(), CaseInsensitiveJson);
                }
                catch (Exception dex)
                {
                    _logger.LogWarning(dex, "iiko_transaction: payload DTO-ya deserialize olunmadı");
                }

                var terminalGroupId = request?.TerminalsGroup?.Id;
                if (request == null || string.IsNullOrWhiteSpace(terminalGroupId))
                    return Ok(new { success = false, message = "TerminalsGroup.Id is required" });

                // internalReq-i ctx-dən əvvəl qur — tenant tapılmasa da uğursuzluğu loglaya bilək.
                var firstCustomer = request.Customer?.FirstOrDefault();

                internalReq = new IikoOrderEventRequest
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
                        DiscountSum = d.DiscountSum,
                        DiscountByItem = (d.DiscountByItem ?? new()).Select(bi => new IikoOrderDiscountByItem
                        {
                            OrderItemId = bi.OrderItemId,
                            ProductId = bi.ProductId,
                            ProductName = bi.ProductName,
                            DiscountSum = bi.DiscountSum
                        }).ToList()
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

                var ctx = await _resolver.ResolveByTerminalGroupIdAsync(terminalGroupId, IikoSource, ct);
                if (ctx == null)
                {
                    _logger.LogWarning(
                        "iiko_transaction: tenant not found. terminalGroupId={TerminalGroupId} orderId={OrderId}",
                        terminalGroupId, request.OrderId);
                    // İnteqrasiya sonradan qurula bilər → retry növbəsinə yaz.
                    // CancellationToken.None: client kəsilsə belə qeyd yazılmalıdır.
                    await _failureService.RecordFailureAsync(internalReq, null, "Tenant integration not found", CancellationToken.None);
                    return Ok(new { success = false, message = "Tenant integration not found" });
                }

                // Deaktiv tenant-da POS auto-earn qadağandır (abunə dayandırılıb) — retry-ə yazmırıq.
                if (!await _tenantStatusGuard.IsActiveAsync(ctx.TenantId, ct))
                {
                    _logger.LogWarning("iiko_transaction blocked — tenant inactive. TenantId={TenantId}", ctx.TenantId);
                    return Ok(new { success = false, message = Business.Services.Implementations.Main.TenantStatusGuard.InactiveMessage });
                }

                // Hangfire-ə enqueue et — emal PosBatchOperationJob-da gedir.
                // Uğursuzluq (customer not found və s.) və exception job daxilində
                // IikoWebhookFailures-ə yazılır → 10 dəqiqəlik retry növbəsi.
                string jobId;
                try
                {
                    jobId = _jobClient.Enqueue<PosBatchOperationJob>(j => j.ProcessAsync(ctx, internalReq, CancellationToken.None));
                }
                catch (Exception enqueueEx)
                {
                    // Hangfire storage əlçatmazdırsa sifarişi itirmə — retry növbəsinə yaz.
                    _logger.LogError(enqueueEx, "iiko_transaction ENQUEUE_FAIL. orderId={OrderId}", request.OrderId);
                    await _failureService.RecordFailureAsync(internalReq, ctx.TenantIntegrationId,
                        "Hangfire enqueue failed: " + enqueueEx.Message, CancellationToken.None);
                    return Ok(new { success = false, message = "Queued for retry" });
                }

                _logger.LogInformation(
                    "iiko_transaction ENQUEUED. jobId={JobId} eventType={EventType} orderId={OrderId} orderNumber={OrderNumber} tenantId={TenantId} totalSum={TotalSum} card={Card}",
                    jobId, internalReq.EventType, request.OrderId, request.OrderNumber, ctx.TenantId, internalReq.TotalSum, internalReq.CustomerCardNumber);

                return Ok(new { success = true, message = "Accepted", jobId });
            }
            catch (Exception ex)
            {
                // Webhook best practice: 200 qaytar ki, iiko sonsuz retry etməsin.
                _logger.LogError(ex, "iiko_transaction unhandled error. orderId={OrderId}", internalReq?.OrderId);
                // Payload artıq parse olunubsa sifarişi itirmə — retry növbəsinə yaz.
                if (internalReq != null)
                    await _failureService.RecordFailureAsync(internalReq, null, "Unhandled: " + ex.Message, CancellationToken.None);
                return Ok(new { success = false, message = "Internal error" });
            }
        }
    }
}
