using System.Text.Json;
using Hangfire;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class IikoWebhookFailureService : IIikoWebhookFailureService
    {
        private const string IikoSource = "iiko_pos";
        private const int MaxAttempts = 20;
        private const int MaxAgeDays = 7;
        private const int BatchSize = 50;

        private readonly IIikoWebhookFailureRepository _failureRepo;
        private readonly ITenantIntegrationResolver _resolver;
        private readonly IWalletWebhookService _webhookService;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<IikoWebhookFailureService> _logger;

        public IikoWebhookFailureService(
            IIikoWebhookFailureRepository failureRepo,
            ITenantIntegrationResolver resolver,
            IWalletWebhookService webhookService,
            IUnitOfWork uow,
            ILogger<IikoWebhookFailureService> logger)
        {
            _failureRepo = failureRepo;
            _resolver = resolver;
            _webhookService = webhookService;
            _uow = uow;
            _logger = logger;
        }

        public async Task RecordFailureAsync(IikoOrderEventRequest req, int? tenantIntegrationId, string? reason, CancellationToken ct = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(req);
                var existing = await _failureRepo.GetByOrderAsync(req.OrderId, req.EventType, ct);
                if (existing != null)
                {
                    if (existing.Status == 1) return; // artıq Resolved — toxunma
                    existing.RawBody = json;
                    existing.FailureReason = reason;
                    existing.LastAttemptAt = DateTime.UtcNow;
                    existing.EditDate = DateTime.UtcNow;
                    _failureRepo.Update(existing);
                }
                else
                {
                    await _failureRepo.AddAsync(new IikoWebhookFailure
                    {
                        TerminalGroupId = req.TerminalGroupId,
                        OrderId = req.OrderId,
                        EventType = req.EventType,
                        RawBody = json,
                        FailureReason = reason,
                        AttemptCount = 0,
                        Status = 0,
                        RegUserId = 1,
                        RegDate = DateTime.UtcNow,
                        IsActive = true
                    });
                }
                await _uow.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko failure log yazıla bilmədi. OrderId={OrderId}", req.OrderId);
            }
        }

        // Hangfire: bütün batch bir işdir — daxili sətir-sətir emal, auto-retry lazım deyil.
        [AutomaticRetry(Attempts = 0)]
        [Queue("default")]
        public async Task RetryPendingAsync(CancellationToken ct = default)
        {
            var minRegDate = DateTime.UtcNow.AddDays(-MaxAgeDays);
            var pending = await _failureRepo.GetPendingAsync(MaxAttempts, minRegDate, BatchSize, ct);
            if (pending.Count == 0) return;

            _logger.LogInformation("iiko webhook retry: {Count} pending", pending.Count);

            foreach (var f in pending)
            {
                try
                {
                    var req = JsonSerializer.Deserialize<IikoOrderEventRequest>(f.RawBody);
                    if (req == null)
                    {
                        f.Status = 2; // parse olunmur — Abandoned
                        f.FailureReason = "Deserialize failed";
                    }
                    else
                    {
                        var ctx = await _resolver.ResolveByTerminalGroupIdAsync(req.TerminalGroupId ?? "", IikoSource, ct);
                        if (ctx == null)
                        {
                            f.AttemptCount++;
                            f.FailureReason = "Tenant integration not found";
                        }
                        else
                        {
                            var res = await _webhookService.ProcessIikoOrderEventAsync(ctx, req, ct);
                            if (res.Success)
                            {
                                f.Status = 1;
                                f.ResolvedAt = DateTime.UtcNow;
                                f.FailureReason = res.Message;
                            }
                            else
                            {
                                f.AttemptCount++;
                                f.FailureReason = res.Message;
                            }
                        }
                    }

                    // Attempt/age limit → Abandoned
                    if (f.Status == 0 && (f.AttemptCount >= MaxAttempts || f.RegDate < minRegDate))
                        f.Status = 2;

                    f.LastAttemptAt = DateTime.UtcNow;
                    f.EditDate = DateTime.UtcNow;
                    _failureRepo.Update(f);
                    await _uow.CommitAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "iiko webhook retry error. FailureId={Id} OrderId={OrderId}", f.Id, f.OrderId);
                }
            }
        }
    }
}
