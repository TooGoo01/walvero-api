using Hangfire;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Implementations.Main;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// POS batch webhook — bir sorğuda bir neçə əməliyyat qəbul edir.
    /// Auth header tələb etmir — tenant payload-dakı organizationId-dən resolve olunur.
    /// Hər operation Hangfire-ə enqueue olunur — HTTP cavab dərhal qayıdır, processing background-da.
    /// </summary>
    [ApiController]
    [Route("api/wallet")]
    [AllowAnonymous]
    public class PosBatchWebhookController : ControllerBase
    {
        private const string IikoSource = "iiko_pos";

        private readonly ITenantIntegrationResolver _resolver;
        private readonly IBackgroundJobClient _jobClient;
        private readonly ILogger<PosBatchWebhookController> _logger;

        public PosBatchWebhookController(
            ITenantIntegrationResolver resolver,
            IBackgroundJobClient jobClient,
            ILogger<PosBatchWebhookController> logger)
        {
            _resolver = resolver;
            _jobClient = jobClient;
            _logger = logger;
        }

        [HttpPost("pos_batch_webhook")]
        public async Task<IActionResult> Receive([FromBody] PosBatchWebhookRequest request, CancellationToken ct)
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
                    "pos batch webhook RECEIVED. traceId={TraceId} clientIp={ClientIp} opCount={OpCount} orgId={OrgId} uocId={UocId}",
                    traceId, clientIp, request?.Operations?.Count ?? 0, request?.OrganizationId, request?.UocId);

                _logger.LogDebug(
                    "pos batch webhook RAW: {RawJson}",
                    JsonSerializer.Serialize(request));

                if (request == null || request.Operations == null || request.Operations.Count == 0)
                {
                    _logger.LogWarning("pos batch webhook VALIDATION FAIL: operations array is required. traceId={TraceId}", traceId);
                    return Ok(new { success = false, message = "operations array is required" });
                }

                if (string.IsNullOrWhiteSpace(request.OrganizationId))
                {
                    _logger.LogWarning("pos batch webhook VALIDATION FAIL: organizationId required. traceId={TraceId}", traceId);
                    return Ok(new { success = false, message = "organizationId is required" });
                }

                var resolveSw = Stopwatch.StartNew();
                var ctx = await _resolver.ResolveByVenueAndSourceAsync(request.OrganizationId, IikoSource, ct);
                bool usedUocFallback = false;
                if (ctx == null && !string.IsNullOrWhiteSpace(request.UocId))
                {
                    ctx = await _resolver.ResolveByVenueAndSourceAsync(request.UocId, IikoSource, ct);
                    usedUocFallback = ctx != null;
                }
                resolveSw.Stop();

                if (ctx == null)
                {
                    _logger.LogWarning(
                        "pos batch webhook TENANT_NOT_FOUND. traceId={TraceId} organizationId={OrgId} uocId={UocId} resolveMs={ResolveMs}",
                        traceId, request.OrganizationId, request.UocId, resolveSw.ElapsedMilliseconds);
                    return Ok(new { success = false, message = "Tenant integration not found" });
                }

                _logger.LogInformation(
                    "pos batch webhook TENANT_RESOLVED. traceId={TraceId} tenantId={TenantId} integrationId={IntegrationId} loyaltyProgramId={LoyaltyProgramId} usedUocFallback={Fallback} resolveMs={ResolveMs}",
                    traceId, ctx.TenantId, ctx.TenantIntegrationId, ctx.LoyaltyProgramId, usedUocFallback, resolveSw.ElapsedMilliseconds);

                int enqueued = 0, failedToEnqueue = 0;
                var enqueueErrors = new List<object>();
                var enqueueSw = Stopwatch.StartNew();

                for (int i = 0; i < request.Operations.Count; i++)
                {
                    var op = request.Operations[i];
                    try
                    {
                        var iikoReq = new IikoTransactionRequest
                        {
                            Sum = op.Sum ?? 0,
                            Balance = op.Balance ?? 0,
                            OrderId = op.OrderId,
                            OrderNumber = op.OrderNumber,
                            IsDelivery = op.IsDelivery,
                            TerminalGroupId = op.TerminalGroupId,
                            WalletId = op.WalletId,
                            Id = op.Id,
                            OrganizationId = request.OrganizationId,
                            UocId = request.UocId,
                            NotificationType = op.NotificationType,
                            CustomerId = op.CustomerId ?? "",
                            Phone = op.Phone,
                            TransactionType = op.TransactionType ?? "",
                            TransactionId = op.TransactionId ?? "",
                            SubscriptionPassword = op.SubscriptionPassword,
                            ChangedOn = op.ChangedOn
                        };

                        var jobId = _jobClient.Enqueue<PosBatchOperationJob>(j =>
                            j.ProcessAsync(ctx, iikoReq, CancellationToken.None));
                        enqueued++;

                        _logger.LogDebug(
                            "pos batch ENQUEUED. traceId={TraceId} index={Idx} jobId={JobId} txId={TxId} customerId={CustomerId} sum={Sum} balance={Balance} type={Type}",
                            traceId, i, jobId, op.TransactionId, op.CustomerId, op.Sum, op.Balance, op.TransactionType);
                    }
                    catch (Exception itemEx)
                    {
                        failedToEnqueue++;
                        _logger.LogError(itemEx,
                            "pos batch ENQUEUE_FAIL. traceId={TraceId} index={Idx} txId={TxId} customerId={CustomerId}",
                            traceId, i, op.TransactionId, op.CustomerId);
                        enqueueErrors.Add(new
                        {
                            index = i,
                            transactionId = op.TransactionId,
                            customerId = op.CustomerId,
                            message = "Failed to enqueue"
                        });
                    }
                }

                enqueueSw.Stop();
                sw.Stop();

                _logger.LogInformation(
                    "pos batch webhook COMPLETED. traceId={TraceId} orgId={OrgId} tenantId={TenantId} total={Total} enqueued={Enqueued} failedToEnqueue={Failed} resolveMs={ResolveMs} enqueueMs={EnqueueMs} totalMs={TotalMs}",
                    traceId, request.OrganizationId, ctx.TenantId, request.Operations.Count, enqueued, failedToEnqueue,
                    resolveSw.ElapsedMilliseconds, enqueueSw.ElapsedMilliseconds, sw.ElapsedMilliseconds);

                return Ok(new
                {
                    success = failedToEnqueue == 0,
                    total = request.Operations.Count,
                    enqueued,
                    failedToEnqueue,
                    message = "Accepted for background processing",
                    errors = enqueueErrors
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "pos batch webhook UNHANDLED_ERROR. traceId={TraceId} totalMs={TotalMs}",
                    traceId, sw.ElapsedMilliseconds);
                return Ok(new { success = false, message = "Internal error" });
            }
        }
    }

    public class PosBatchWebhookRequest
    {
        [JsonPropertyName("organizationId")]
        public string? OrganizationId { get; set; }

        [JsonPropertyName("uocId")]
        public string? UocId { get; set; }

        [JsonPropertyName("operations")]
        public List<PosBatchOperation> Operations { get; set; } = new();
    }

    public class PosBatchOperation
    {
        public decimal? Sum { get; set; }
        public decimal? Balance { get; set; }
        public string? OrderId { get; set; }
        public int? OrderNumber { get; set; }
        public bool? IsDelivery { get; set; }
        public string? TerminalGroupId { get; set; }
        public string? WalletId { get; set; }
        public string? Id { get; set; }
        public int? NotificationType { get; set; }
        public string? CustomerId { get; set; }
        public string? Phone { get; set; }
        public string? TransactionType { get; set; }
        public string? TransactionId { get; set; }
        public string? SubscriptionPassword { get; set; }
        public string? ChangedOn { get; set; }
    }
}
