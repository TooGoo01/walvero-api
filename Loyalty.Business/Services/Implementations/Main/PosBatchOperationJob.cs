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

            try
            {
                var result = await service.ProcessIikoTransactionAsync(ctx, req, ct);
                _logger.LogInformation(
                    "pos batch job processed: txId={TxId} customerId={CustomerId} success={Success} message={Message}",
                    req.TransactionId, req.CustomerId, result.Success, result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "pos batch job error. txId={TxId} customerId={CustomerId}",
                    req.TransactionId, req.CustomerId);
                throw;
            }
        }
    }
}
