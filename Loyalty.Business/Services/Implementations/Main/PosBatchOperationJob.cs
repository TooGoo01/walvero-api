using System.Diagnostics;
using Hangfire;
using Loyalty.Business.Helpers;
using Loyalty.Business.Models.TenantIntegration;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// iiko POS OrderClosed / OrderReturned eventini background-da işləyir.
    /// Hangfire dequeue → IWalletWebhookService.ProcessIikoOrderEventAsync.
    /// Uğursuzluq siyasəti: keçici exception → Hangfire retry (3×, 10s/60s/300s) + IikoWebhookFailures (Transient);
    /// daimi exception → yalnız IikoWebhookFailures (Rejected), job Succeeded bitir — mənasız retry yoxdur.
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
        [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 60, 300 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
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
                    // İnteqrasiya/venue sonradan qurula bilər → yavaş retry (Retriable).
                    await failureService.RecordFailureAsync(req, null, "Tenant integration not found", IikoFailureKind.Retriable, CancellationToken.None);
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
                var transient = TransientErrorClassifier.IsTransient(ex);
                _logger.LogError(ex, "iiko order event RESOLVE_EXCEPTION. terminalGroupId={TerminalGroupId} orderId={OrderId} transient={Transient}",
                    req.TerminalGroupId, req.OrderId, transient);
                await failureService.RecordFailureAsync(req, null, "Resolve exception: " + ex.Message,
                    transient ? IikoFailureKind.Transient : IikoFailureKind.Permanent, CancellationToken.None);
                if (transient) throw; // Hangfire retry
                return;               // daimi xəta — retry mənasızdır
            }

            await ProcessAsync(ctx, req, ct);
        }

        [Queue("default")]
        [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 60, 300 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
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
                    var kind = result.FailureKind == IikoFailureKind.None ? IikoFailureKind.Retriable : result.FailureKind;
                    _logger.LogWarning(
                        "iiko order event job FAILED_LOGIC. eventType={EventType} orderId={OrderId} elapsedMs={ElapsedMs} kind={Kind} message={Message}",
                        req.EventType, req.OrderId, sw.ElapsedMilliseconds, kind, result.Message);

                    // Customer not found (Retriable, günlərlə yavaş retry), lock timeout (Transient), OrderId boş (Permanent → Rejected).
                    await failureService.RecordFailureAsync(req, ctx.TenantIntegrationId, result.Message, kind, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                var transient = TransientErrorClassifier.IsTransient(ex);
                _logger.LogError(ex,
                    "iiko order event job EXCEPTION. eventType={EventType} orderId={OrderId} elapsedMs={ElapsedMs} transient={Transient}",
                    req.EventType, req.OrderId, sw.ElapsedMilliseconds, transient);

                await failureService.RecordFailureAsync(req, ctx.TenantIntegrationId, "Exception: " + ex.Message,
                    transient ? IikoFailureKind.Transient : IikoFailureKind.Permanent, CancellationToken.None);

                // Keçici (DB timeout/deadlock/şəbəkə) → Hangfire retry; per-order lock + idempotency ikiqat emalı bloklayır.
                // Daimi (NullReference, məlumat xətası) → retry mənasızdır, job burada bitir.
                if (transient) throw;
            }

            if (string.Equals(req.EventType, "OrderClosed", StringComparison.OrdinalIgnoreCase) && req.Items?.Count > 0)
                await RecordObservedProductsAsync(ctx.TenantIntegrationId, req, ct);
        }

        /// <summary>
        /// Sifarişdəki məhsulları super admin "bonus düşməyən məhsullar" seçimi üçün siyahıya yazır.
        /// Ayrıca scope (sifariş DbContext-indən asılı deyil); xəta sifariş nəticəsinə təsir etmir.
        /// </summary>
        private async Task RecordObservedProductsAsync(int tenantIntegrationId, IikoOrderEventRequest req, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var recorder = scope.ServiceProvider.GetRequiredService<IPosObservedProductRecorder>();
                await recorder.RecordAsync(tenantIntegrationId, req.Items, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Observed products record failed. TenantIntegrationId={Id} orderId={OrderId}",
                    tenantIntegrationId, req.OrderId);
            }
        }
    }
}
