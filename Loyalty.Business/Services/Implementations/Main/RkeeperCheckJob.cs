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
    /// r_keeper çekinin (TransactionsEx) background emalı — kassa cavabı gözləmir (ack-first).
    /// "pos" növbəsində işləyir: bu növbənin öz Hangfire serveri var, çek OCR ("ocr") və push/bildiriş ("default")
    /// worker-lərini tutmur. Keçici xəta (DB timeout/deadlock) → Hangfire retry (3×); idempotency (PosOrderMarkers)
    /// və per-order lock ikiqat emalı bloklayır. Məntiq xətası / daimi xəta → yalnız log (retry mənasızdır).
    /// </summary>
    public sealed class RkeeperCheckJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RkeeperCheckJob> _logger;

        public RkeeperCheckJob(IServiceScopeFactory scopeFactory, ILogger<RkeeperCheckJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [Queue("pos")]
        [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 60, 300 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task ProcessAsync(WalletWebhookResolvedContext ctx, IikoOrderEventRequest req, CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IWalletWebhookService>();

            var sw = Stopwatch.StartNew();
            try
            {
                var result = await service.ProcessIikoOrderEventAsync(ctx, req, ct);
                if (result.Success)
                    _logger.LogInformation(
                        "rkeeper check job SUCCEEDED. TenantId={TenantId} EventType={EventType} OrderId={OrderId} BranchId={BranchId} ElapsedMs={ElapsedMs} Message={Message}",
                        ctx.TenantId, req.EventType, req.OrderId, ctx.BranchId, sw.ElapsedMilliseconds, result.Message);
                else
                    _logger.LogError(
                        "rkeeper check job FAILED_LOGIC. TenantId={TenantId} EventType={EventType} OrderId={OrderId} Kind={Kind} ElapsedMs={ElapsedMs} Message={Message}",
                        ctx.TenantId, req.EventType, req.OrderId, result.FailureKind, sw.ElapsedMilliseconds, result.Message);
            }
            catch (Exception ex)
            {
                var transient = TransientErrorClassifier.IsTransient(ex);
                _logger.LogError(ex,
                    "rkeeper check job EXCEPTION. TenantId={TenantId} EventType={EventType} OrderId={OrderId} ElapsedMs={ElapsedMs} Transient={Transient}",
                    ctx.TenantId, req.EventType, req.OrderId, sw.ElapsedMilliseconds, transient);
                if (transient) throw; // Hangfire retry
            }
        }
    }
}
