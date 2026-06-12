using Hangfire;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Implementations.Main;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            try
            {
                _logger.LogInformation(
                    "pos batch webhook RAW: {RawJson}",
                    JsonSerializer.Serialize(request));

                if (request == null || request.Operations == null || request.Operations.Count == 0)
                    return Ok(new { success = false, message = "operations array is required" });

                if (string.IsNullOrWhiteSpace(request.OrganizationId))
                    return Ok(new { success = false, message = "organizationId is required" });

                var ctx = await _resolver.ResolveByVenueAndSourceAsync(request.OrganizationId, IikoSource, ct);
                if (ctx == null && !string.IsNullOrWhiteSpace(request.UocId))
                    ctx = await _resolver.ResolveByVenueAndSourceAsync(request.UocId, IikoSource, ct);

                if (ctx == null)
                {
                    _logger.LogWarning(
                        "pos batch webhook: tenant not found. organizationId={OrgId} uocId={UocId}",
                        request.OrganizationId, request.UocId);
                    return Ok(new { success = false, message = "Tenant integration not found" });
                }

                int enqueued = 0, failedToEnqueue = 0;
                var enqueueErrors = new List<object>();

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

                        _jobClient.Enqueue<PosBatchOperationJob>(j =>
                            j.ProcessAsync(ctx, iikoReq, CancellationToken.None));
                        enqueued++;
                    }
                    catch (Exception itemEx)
                    {
                        failedToEnqueue++;
                        _logger.LogError(itemEx,
                            "pos batch enqueue error. index={Idx} txId={TxId} customerId={CustomerId}",
                            i, op.TransactionId, op.CustomerId);
                        enqueueErrors.Add(new
                        {
                            index = i,
                            transactionId = op.TransactionId,
                            customerId = op.CustomerId,
                            message = "Failed to enqueue"
                        });
                    }
                }

                _logger.LogInformation(
                    "pos batch webhook enqueued: orgId={OrgId} total={Total} enqueued={Enqueued} failed={Failed}",
                    request.OrganizationId, request.Operations.Count, enqueued, failedToEnqueue);

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
                _logger.LogError(ex, "pos batch webhook unhandled error");
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
