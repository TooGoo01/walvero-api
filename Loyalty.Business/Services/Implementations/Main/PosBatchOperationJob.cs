using System.Diagnostics;
using Hangfire;
using Loyalty.Business.Models.TenantIntegration;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// iiko POS OrderClosed / OrderReturned eventini background-da işləyir.
    /// Hangfire dequeue → IWalletWebhookService.ProcessIikoOrderEventAsync.
    /// </summary>
    public sealed class PosBatchOperationJob
    {
        private const string IikoSource = "iiko_pos";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PosBatchOperationJob> _logger;

        public PosBatchOperationJob(
            IServiceScopeFactory scopeFactory,
            ILogger<PosBatchOperationJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// Ack-first giriş (iiko_transaction): controller cavabdan əvvəl heç bir DB sorğusu etmir —
        /// iiko plugin-in 5s timeout-una görə. Tenant resolve + status guard burada, background-da gedir.
        /// </summary>
        [Queue("default")]
        [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task ProcessByTerminalGroupAsync(IikoOrderEventRequest req, CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<ITenantIntegrationResolver>();
            var statusGuard = scope.ServiceProvider.GetRequiredService<ITenantStatusGuard>();
            var failureService = scope.ServiceProvider.GetRequiredService<IIikoWebhookFailureService>();

            WalletWebhookResolvedContext? ctx;
            try
            {
                ctx = await resolver.ResolveByTerminalGroupIdAsync(req.TerminalGroupId ?? string.Empty, IikoSource, ct);
                if (ctx == null)
                {
                    _logger.LogWarning(
                        "iiko order event: tenant not found. terminalGroupId={TerminalGroupId} orderId={OrderId}",
                        req.TerminalGroupId, req.OrderId);
                    // İnteqrasiya sonradan qurula bilər → retry növbəsinə yaz.
                    await failureService.RecordFailureAsync(req, null, "Tenant integration not found", CancellationToken.None);
                    return;
                }

                // Deaktiv tenant-da POS auto-earn qadağandır (abunə dayandırılıb) — retry-ə yazmırıq.
                if (!await statusGuard.IsActiveAsync(ctx.TenantId, ct))
                {
                    _logger.LogWarning(
                        "iiko order event blocked — tenant inactive. TenantId={TenantId} orderId={OrderId}",
                        ctx.TenantId, req.OrderId);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "iiko order event RESOLVE_EXCEPTION. terminalGroupId={TerminalGroupId} orderId={OrderId}",
                    req.TerminalGroupId, req.OrderId);
                await failureService.RecordFailureAsync(req, null, "Resolve exception: " + ex.Message, CancellationToken.None);
                throw; // Hangfire retry
            }

            await ProcessAsync(ctx, req, ct);
        }

        [Queue("default")]
        [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task ProcessAsync(
            WalletWebhookResolvedContext ctx,
            IikoOrderEventRequest req,
            CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IWalletWebhookService>();
            var failureService = scope.ServiceProvider.GetRequiredService<IIikoWebhookFailureService>();

            using var logScope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["Job"] = "PosBatchOperationJob",
                ["TenantId"] = ctx.TenantId,
                ["IntegrationId"] = ctx.TenantIntegrationId,
                ["EventType"] = req.EventType ?? string.Empty,
                ["OrderId"] = req.OrderId ?? string.Empty,
                ["ExtCustId"] = req.ExternalCustomerId ?? string.Empty
            });

            var sw = Stopwatch.StartNew();

            _logger.LogInformation(
                "iiko order event job STARTED. eventType={EventType} orderId={OrderId} orderNumber={OrderNumber} totalSum={TotalSum} customerId={ExtCustId} card={CardNumber} itemCount={ItemCount} paymentCount={PayCount}",
                req.EventType, req.OrderId, req.OrderNumber, req.TotalSum,
                req.ExternalCustomerId, req.CustomerCardNumber,
                req.Items?.Count ?? 0, req.Payments?.Count ?? 0);

            try
            {
                var result = await service.ProcessIikoOrderEventAsync(ctx, req, ct);
                sw.Stop();

                if (result.Success)
                {
                    _logger.LogInformation(
                        "iiko order event job SUCCEEDED. eventType={EventType} orderId={OrderId} elapsedMs={ElapsedMs} message={Message}",
                        req.EventType, req.OrderId, sw.ElapsedMilliseconds, result.Message);
                }
                else
                {
                    _logger.LogWarning(
                        "iiko order event job FAILED_LOGIC. eventType={EventType} orderId={OrderId} elapsedMs={ElapsedMs} message={Message}",
                        req.EventType, req.OrderId, sw.ElapsedMilliseconds, result.Message);

                    // Customer not found və s. → IikoWebhookFailures (10 dəq retry, müştəri sonradan sync olunanda keçsin).
                    await failureService.RecordFailureAsync(req, ctx.TenantIntegrationId, result.Message, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "iiko order event job EXCEPTION. eventType={EventType} orderId={OrderId} elapsedMs={ElapsedMs}",
                    req.EventType, req.OrderId, sw.ElapsedMilliseconds);

                // Hangfire 3 dəfə retry edəcək; hər halda failure cədvəlinə də yaz (idempotency key ikiqat emalı bloklar).
                await failureService.RecordFailureAsync(req, ctx.TenantIntegrationId, "Exception: " + ex.Message, CancellationToken.None);
                throw;
            }
        }
    }
}
