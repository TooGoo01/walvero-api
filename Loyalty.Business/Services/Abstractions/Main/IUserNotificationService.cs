namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Customer-tərəfə bildirişlər (FCM push + Notification entity persist).
    /// Receipt OCR / QR flag-lərində istifadəçiyə uyğunsuzluq haqqında məlumat verir.
    /// </summary>
    public interface IUserNotificationService
    {
        Task NotifyReceiptIssueAsync(
            int customerId,
            string tenantName,
            string reason,
            CancellationToken ct = default);
    }
}
