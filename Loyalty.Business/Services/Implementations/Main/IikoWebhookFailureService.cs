using System.Text.Json;
using Hangfire;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class IikoWebhookFailureService : IIikoWebhookFailureService
    {
        private const string IikoSource = "iiko_pos";
        private const int MaxAgeDays = 7;
        private const int BatchSize = 50;

        // Backoff cədvəlləri: index = artıq edilmiş cəhd sayı; uzunluq = maksimum cəhd.
        // Transient (DB timeout, deadlock, lock): tez və qısa. Retriable (müştəri/konfiq yoxdur): yavaş, 7 günə qədər.
        private static readonly TimeSpan[] TransientBackoff =
        {
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(30), TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.FromHours(1)
        };
        private static readonly TimeSpan[] RetriableBackoff =
        {
            TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(80),
            TimeSpan.FromMinutes(160), TimeSpan.FromHours(5), TimeSpan.FromHours(24), TimeSpan.FromHours(24),
            TimeSpan.FromHours(24), TimeSpan.FromHours(24)
        };

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

        // ===================== qeyd =====================

        public async Task RecordFailureAsync(IikoOrderEventRequest req, int? tenantIntegrationId, string? reason, IikoFailureKind kind, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.OrderId))
            {
                // OrderId-siz payload heç vaxt emal oluna bilməz — sətir yazmaq mənasızdır (əvvəl hər çağırış yeni sətir yaradırdı).
                _logger.LogWarning("iiko failure: OrderId boşdur, növbəyə yazılmadı. terminalGroupId={TerminalGroupId} reason={Reason}",
                    req.TerminalGroupId, reason);
                return;
            }

            try
            {
                var now = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(req);
                var existing = await _failureRepo.GetByOrderAsync(req.OrderId, req.EventType, ct);
                IikoWebhookFailure row;
                if (existing != null)
                {
                    // Resolved / Rejected sətrə toxunma (Rejected yalnız əl ilə requeue ilə açılır).
                    if (existing.Status == IikoWebhookFailureStatus.Resolved || existing.Status == IikoWebhookFailureStatus.Rejected)
                        return;
                    existing.RawBody = json;
                    existing.FailureReason = reason;
                    existing.LastAttemptAt = now;
                    existing.EditDate = now;
                    ApplyKindOnRecord(existing, kind, now);
                    _failureRepo.Update(existing);
                    row = existing;
                }
                else
                {
                    row = new IikoWebhookFailure
                    {
                        TerminalGroupId = req.TerminalGroupId,
                        OrderId = req.OrderId,
                        EventType = req.EventType,
                        RawBody = json,
                        FailureReason = reason,
                        AttemptCount = 0,
                        RegUserId = 1,
                        RegDate = now,
                        IsActive = true
                    };
                    ApplyKindOnRecord(row, kind, now);
                    await _failureRepo.AddAsync(row);
                }
                await _uow.CommitAsync(ct);

                _logger.LogInformation(
                    "iiko failure recorded. OrderId={OrderId} eventType={EventType} kind={Kind} status={Status} nextAttemptAt={NextAttemptAt} reason={Reason}",
                    req.OrderId, req.EventType, kind, row.Status, row.NextAttemptAt, reason);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko failure log yazıla bilmədi. OrderId={OrderId}", req.OrderId);
            }
        }

        /// <summary>Yeni uğursuzluq siqnalı: Permanent → Rejected; əks halda Pending + backoff (Abandoned sətir təzə siqnalla yenidən açılır).</summary>
        private static void ApplyKindOnRecord(IikoWebhookFailure f, IikoFailureKind kind, DateTime now)
        {
            kind = Normalize(kind);
            f.FailureKind = (int)kind;
            if (kind == IikoFailureKind.Permanent)
            {
                f.Status = IikoWebhookFailureStatus.Rejected;
                f.NextAttemptAt = null;
                return;
            }
            f.Status = IikoWebhookFailureStatus.Pending;
            f.NextAttemptAt = now + Backoff(kind, f.AttemptCount);
        }

        // ===================== retry =====================

        // Hangfire: bütün batch bir işdir — daxili sətir-sətir emal, auto-retry lazım deyil.
        [AutomaticRetry(Attempts = 0)]
        [Queue("default")]
        public async Task RetryPendingAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var minRegDate = now.AddDays(-MaxAgeDays);
            var due = await _failureRepo.GetDueAsync(now, minRegDate, BatchSize, ct);
            if (due.Count == 0) return;

            _logger.LogInformation("iiko webhook retry: {Count} due", due.Count);

            foreach (var f in due)
            {
                var attemptAt = DateTime.UtcNow;
                try
                {
                    var req = JsonSerializer.Deserialize<IikoOrderEventRequest>(f.RawBody);
                    if (req == null)
                    {
                        MarkFailed(f, IikoFailureKind.Permanent, "Deserialize failed", attemptAt, minRegDate);
                    }
                    else
                    {
                        var ctx = await _resolver.ResolveByTerminalGroupIdAsync(req.TerminalGroupId ?? "", IikoSource, ct);
                        if (ctx == null)
                        {
                            MarkFailed(f, IikoFailureKind.Retriable, "Tenant integration not found", attemptAt, minRegDate);
                        }
                        else
                        {
                            var res = await _webhookService.ProcessIikoOrderEventAsync(ctx, req, ct);
                            if (res.Success)
                                MarkResolved(f, res.Message, attemptAt);
                            else
                                MarkFailed(f, res.FailureKind, res.Message, attemptAt, minRegDate);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var kind = TransientErrorClassifier.IsTransient(ex) ? IikoFailureKind.Transient : IikoFailureKind.Permanent;
                    _logger.LogWarning(ex, "iiko webhook retry exception. FailureId={Id} OrderId={OrderId} kind={Kind}", f.Id, f.OrderId, kind);
                    MarkFailed(f, kind, "Exception: " + ex.Message, attemptAt, minRegDate);
                }

                try
                {
                    f.LastAttemptAt = attemptAt;
                    f.EditDate = attemptAt;
                    _failureRepo.Update(f);
                    await _uow.CommitAsync(ct);

                    _logger.LogInformation(
                        "iiko webhook retry result. FailureId={Id} OrderId={OrderId} status={Status} kind={Kind} attempts={Attempts} nextAttemptAt={NextAttemptAt} reason={Reason}",
                        f.Id, f.OrderId, f.Status, f.FailureKind, f.AttemptCount, f.NextAttemptAt, f.FailureReason);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "iiko webhook retry: sətir yenilənmədi. FailureId={Id} OrderId={OrderId}", f.Id, f.OrderId);
                }
            }
        }

        private static void MarkResolved(IikoWebhookFailure f, string? message, DateTime now)
        {
            f.Status = IikoWebhookFailureStatus.Resolved;
            f.ResolvedAt = now;
            f.FailureReason = message;
            f.NextAttemptAt = null;
        }

        /// <summary>Retry dövrəsində uğursuzluq: cəhd sayılır, növə görə növbəti vaxt; limit → Abandoned; Permanent → Rejected.</summary>
        private static void MarkFailed(IikoWebhookFailure f, IikoFailureKind kind, string? reason, DateTime now, DateTime minRegDate)
        {
            kind = Normalize(kind);
            f.FailureReason = reason;
            f.FailureKind = (int)kind;

            if (kind == IikoFailureKind.Permanent)
            {
                f.Status = IikoWebhookFailureStatus.Rejected;
                f.NextAttemptAt = null;
                return;
            }

            f.AttemptCount++;
            if (f.AttemptCount >= MaxAttempts(kind) || f.RegDate < minRegDate)
            {
                f.Status = IikoWebhookFailureStatus.Abandoned;
                f.NextAttemptAt = null;
                return;
            }

            f.Status = IikoWebhookFailureStatus.Pending;
            f.NextAttemptAt = now + Backoff(kind, f.AttemptCount);
        }

        /// <summary>None / köhnə 0 → Retriable (ən təhlükəsiz default).</summary>
        private static IikoFailureKind Normalize(IikoFailureKind kind) =>
            kind == IikoFailureKind.None ? IikoFailureKind.Retriable : kind;

        private static TimeSpan Backoff(IikoFailureKind kind, int attemptsDone)
        {
            var table = kind == IikoFailureKind.Transient ? TransientBackoff : RetriableBackoff;
            var idx = Math.Clamp(attemptsDone, 0, table.Length - 1);
            return table[idx];
        }

        private static int MaxAttempts(IikoFailureKind kind) =>
            kind == IikoFailureKind.Transient ? TransientBackoff.Length : RetriableBackoff.Length;

        // ===================== super admin =====================

        public async Task<List<IikoWebhookFailureDto>> ListAsync(int? status, string? terminalGroupId, int take, CancellationToken ct = default)
        {
            var rows = await _failureRepo.ListAsync(status, terminalGroupId, Math.Clamp(take, 1, 500), ct);
            return rows.Select(r => Fill(new IikoWebhookFailureDto(), r)).ToList();
        }

        public async Task<IikoWebhookFailureDetailDto?> GetAsync(int id, CancellationToken ct = default)
        {
            var row = await _failureRepo.GetByIdAsync(id, ct);
            if (row == null) return null;
            var dto = (IikoWebhookFailureDetailDto)Fill(new IikoWebhookFailureDetailDto(), row);
            dto.RawBody = row.RawBody;
            return dto;
        }

        public async Task<bool> RequeueAsync(int id, CancellationToken ct = default)
        {
            var row = await _failureRepo.GetByIdAsync(id, ct);
            if (row == null) return false;
            Requeue(row, DateTime.UtcNow);
            _failureRepo.Update(row);
            await _uow.CommitAsync(ct);
            _logger.LogInformation("iiko failure requeued manually. FailureId={Id} OrderId={OrderId}", row.Id, row.OrderId);
            return true;
        }

        public async Task<int> RequeueByTerminalGroupAsync(string terminalGroupId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(terminalGroupId)) return 0;
            var statuses = new List<int>
            {
                IikoWebhookFailureStatus.Pending, IikoWebhookFailureStatus.Abandoned, IikoWebhookFailureStatus.Rejected
            };
            var rows = await _failureRepo.GetByTerminalGroupAsync(terminalGroupId.Trim(), statuses, ct);
            if (rows.Count == 0) return 0;
            var now = DateTime.UtcNow;
            foreach (var r in rows)
            {
                Requeue(r, now);
                _failureRepo.Update(r);
            }
            await _uow.CommitAsync(ct);
            _logger.LogInformation("iiko failures requeued by terminal. terminalGroupId={TerminalGroupId} count={Count}", terminalGroupId, rows.Count);
            return rows.Count;
        }

        private static void Requeue(IikoWebhookFailure f, DateTime now)
        {
            f.Status = IikoWebhookFailureStatus.Pending;
            f.AttemptCount = 0;
            f.NextAttemptAt = now;      // növbəti retry dövrəsində dərhal
            f.ResolvedAt = null;
            f.EditDate = now;
            f.FailureReason = "Requeued manually" + (string.IsNullOrEmpty(f.FailureReason) ? "" : " | " + f.FailureReason);
        }

        private static IikoWebhookFailureDto Fill(IikoWebhookFailureDto dto, IikoWebhookFailure r)
        {
            dto.Id = r.Id;
            dto.TerminalGroupId = r.TerminalGroupId;
            dto.OrderId = r.OrderId;
            dto.EventType = r.EventType;
            dto.Status = r.Status;
            dto.StatusName = r.Status switch
            {
                IikoWebhookFailureStatus.Pending => "Pending",
                IikoWebhookFailureStatus.Resolved => "Resolved",
                IikoWebhookFailureStatus.Abandoned => "Abandoned",
                IikoWebhookFailureStatus.Rejected => "Rejected",
                _ => r.Status.ToString()
            };
            dto.FailureKind = r.FailureKind;
            dto.FailureKindName = r.FailureKind switch
            {
                IikoWebhookFailureKind.Transient => "Transient",
                IikoWebhookFailureKind.Retriable => "Retriable",
                IikoWebhookFailureKind.Permanent => "Permanent",
                _ => "Unknown"
            };
            dto.AttemptCount = r.AttemptCount;
            dto.FailureReason = r.FailureReason;
            dto.RegDate = r.RegDate;
            dto.LastAttemptAt = r.LastAttemptAt;
            dto.NextAttemptAt = r.NextAttemptAt;
            dto.ResolvedAt = r.ResolvedAt;
            return dto;
        }
    }
}
