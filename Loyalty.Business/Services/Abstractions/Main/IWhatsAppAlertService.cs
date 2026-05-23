namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IWhatsAppAlertService
    {
        /// <summary>
        /// Suspicious-tx hadisəsi haqqında tenant admin-inin WhatsApp nömrəsinə xəbərdarlıq mesajı göndərir.
        /// Send failure caller-ı bloklamır — implementation öz daxilində log edib silently fail edir.
        /// </summary>
        Task SendSuspiciousTxAlertAsync(
            string toPhone,
            string tenantName,
            string? branchName,
            string? customerName,
            string? operatorName,
            decimal enteredAmount,
            decimal? ocrAmount,
            decimal? cap,
            string? suspiciousReason,
            string txReference,
            CancellationToken ct = default);
    }
}
