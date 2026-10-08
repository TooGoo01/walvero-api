using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>Çağıranın konteksti: tenant (token-dən, header yalnız super admin), user, filial məhdudiyyəti (Branch Admin/Moderator).</summary>
    public sealed record TenantNotificationContext(int TenantId, int UserId, int? BranchScopeId);

    public sealed class SendTenantNotificationRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        /// <summary>Geriyə uyğunluq (köhnə UI): Audience.BranchId / CustomerId ilə eynidir.</summary>
        public int? BranchId { get; set; }
        public int? CustomerId { get; set; }
        public bool ToApp { get; set; } = true;
        public bool ToWallet { get; set; }
        public AudienceFilterDto? Audience { get; set; }
        /// <summary>Tenant yerli vaxtı ("2026-10-08T10:00"). null = indi göndər.</summary>
        public DateTime? ScheduledAtLocal { get; set; }
    }

    public sealed record NotificationQuotaDto(int? Limit, int Used, int? Remaining);

    public sealed class BirthdaySettingsDto
    {
        public bool Enabled { get; set; }
        public int SendHourLocal { get; set; } = 10;
        /// <summary>0 = ad günü günü, 1–14 = N gün əvvəl.</summary>
        public int DaysBefore { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool ToApp { get; set; } = true;
        public bool ToWallet { get; set; } = true;
        public bool GiftEnabled { get; set; }
        /// <summary>1 = Points (₼), 2 = Stamps (möhür), 3 = FreeItem (pulsuz məhsul).</summary>
        public BirthdayGiftType GiftType { get; set; } = BirthdayGiftType.Points;
        public decimal GiftValue { get; set; }
        /// <summary>Yalnız Points.</summary>
        public int? GiftExpiryDays { get; set; }
        /// <summary>{hədiyyə} üçün sərbəst mətn (≤100); boş = avtomatik.</summary>
        public string? GiftLabel { get; set; }
    }

    /// <summary>Tenant admin bildirişləri v2: filtrli/planlı göndəriş, kvota, tarixçə, ad günü ayarları.</summary>
    public interface ITenantNotificationService
    {
        Task<ServiceResult> GetQuotaAsync(int tenantId, CancellationToken ct = default);
        Task<ServiceResult> PreviewAudienceAsync(TenantNotificationContext ctx, AudienceFilterDto filter, CancellationToken ct = default);
        Task<ServiceResult> SendOrScheduleAsync(TenantNotificationContext ctx, SendTenantNotificationRequest request, CancellationToken ct = default);
        Task<ServiceResult> ListScheduledAsync(TenantNotificationContext ctx, CancellationToken ct = default);
        Task<ServiceResult> UpdateScheduledAsync(TenantNotificationContext ctx, int id, SendTenantNotificationRequest request, CancellationToken ct = default);
        Task<ServiceResult> CancelScheduledAsync(TenantNotificationContext ctx, int id, CancellationToken ct = default);
        /// <summary>Tarixçə + geri dönüş: hər göndəriş üçün alıcıların sonrakı <paramref name="windowDays"/> gündəki ziyarət/dövriyyəsi.</summary>
        Task<ServiceResult> HistoryAsync(TenantNotificationContext ctx, int page, int pageSize, int windowDays = 7, CancellationToken ct = default);
        Task<ServiceResult> GetBirthdaySettingsAsync(int tenantId, CancellationToken ct = default);
        Task<ServiceResult> SaveBirthdaySettingsAsync(TenantNotificationContext ctx, BirthdaySettingsDto dto, CancellationToken ct = default);
        Task<ServiceResult> UpcomingBirthdaysAsync(int tenantId, int days, CancellationToken ct = default);
        /// <summary>Ad günü kampaniyası nəticələri: son <paramref name="days"/> gün — göndəriş, hədiyyə, geri dönüş, bonus istifadəsi.</summary>
        Task<ServiceResult> BirthdayStatsAsync(int tenantId, int days = 365, int windowDays = 7, CancellationToken ct = default);
    }

    /// <summary>Hangfire recurring job-ları (Program.cs). onlyTenantId — super admin əl ilə test (RunJobsNow) üçün.</summary>
    public interface INotificationJobs
    {
        Task RunScheduledAsync(int? onlyTenantId, CancellationToken ct = default);
        Task RunBirthdaysAsync(int? onlyTenantId, bool ignoreSendHour, CancellationToken ct = default);
        /// <summary>
        /// TEST (super admin RunJobsNow): tək müştəriyə ad günü axını — ayar söndürülü olsa da, tarix uyğun gəlməsə də.
        /// Ayar sətri olmalıdır (mətn/hədiyyə oradan). Real müştərilərə toxunmur.
        /// </summary>
        Task<string> RunBirthdayForCustomerAsync(int tenantId, int customerId, CancellationToken ct = default);
    }
}
