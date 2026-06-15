using System.Diagnostics;
using Hangfire;
using Loyalty.Business.Models.TenantIntegration;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class PosBatchOperationJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PosBatchOperationJob> _logger;

        public PosBatchOperationJob(
            IServiceScopeFactory scopeFactory,
            ILogger<PosBatchOperationJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [Queue("default")]
        [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task ProcessAsync(
            WalletWebhookResolvedContext ctx,
            IikoTransactionRequest req,
            CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IWalletWebhookService>();

            using var logScope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["Job"] = "PosBatchOperationJob",
                ["TenantId"] = ctx.TenantId,
                ["IntegrationId"] = ctx.TenantIntegrationId,
                ["TxId"] = req.TransactionId,
                ["CustomerId"] = req.CustomerId,
                ["WalletId"] = req.WalletId ?? string.Empty
            });

            var sw = Stopwatch.StartNew();

            _logger.LogInformation(
                "pos batch job STARTED. txId={TxId} customerId={CustomerId} walletId={WalletId} sum={Sum} balance={Balance} type={Type} orderId={OrderId}",
                req.TransactionId, req.CustomerId, req.WalletId, req.Sum, req.Balance, req.TransactionType, req.OrderId);

            try
            {
                var result = await service.ProcessIikoTransactionAsync(ctx, req, ct);
                sw.Stop();

                if (result.Success)
                {
                    _logger.LogInformation(
                        "pos batch job SUCCEEDED. txId={TxId} customerId={CustomerId} elapsedMs={ElapsedMs} message={Message}",
                        req.TransactionId, req.CustomerId, sw.ElapsedMilliseconds, result.Message);
                }
                else
                {
                    _logger.LogWarning(
                        "pos batch job FAILED_LOGIC. txId={TxId} customerId={CustomerId} elapsedMs={ElapsedMs} message={Message}",
                        req.TransactionId, req.CustomerId, sw.ElapsedMilliseconds, result.Message);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex,
                    "pos batch job EXCEPTION. txId={TxId} customerId={CustomerId} elapsedMs={ElapsedMs}",
                    req.TransactionId, req.CustomerId, sw.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
