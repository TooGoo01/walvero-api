using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Infrastructure.Extensions;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class TwilioWhatsAppAlertService : IWhatsAppAlertService
    {
        private readonly ILogger<TwilioWhatsAppAlertService> _logger;

        public TwilioWhatsAppAlertService(ILogger<TwilioWhatsAppAlertService> logger)
        {
            _logger = logger;
        }

        public async Task SendSuspiciousTxAlertAsync(
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
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(toPhone))
            {
                _logger.LogWarning("Suspicious tx alert skipped — no toPhone configured. Tx={Tx}", txReference);
                return;
            }

            var reasonLabel = suspiciousReason switch
            {
                "OcrMismatch" => "OCR uyğunsuzluğu",
                "OcrUnreadable" => "OCR oxunmadı",
                "AmountExceedsCap" => "Məbləğ limiti aşıb",
                "SpendCountExceedsCap" => "Xərcləmə sayı limiti aşıb",
                _ => suspiciousReason ?? "Naməlum",
            };

            var lines = new List<string>
            {
                $"⚠️ Şübhəli əməliyyat — {tenantName}",
                $"Filial: {branchName ?? "—"}",
                $"Səbəb: {reasonLabel}",
                $"Müştəri: {customerName ?? "—"}",
                $"Operator: {operatorName ?? "—"}",
                $"Daxil edilən məbləğ: {enteredAmount:0.##} AZN",
            };
            if (ocrAmount.HasValue)
                lines.Add($"Çekdən oxunan (OCR): {ocrAmount.Value:0.##} AZN");
            if (cap.HasValue && cap.Value > 0)
                lines.Add($"Limit (cap): {cap.Value:0.##} AZN");
            lines.Add($"Tx: {txReference}");

            var body = string.Join("\n", lines);

            try
            {
                await WhatsAppHelper.SendWhatsAppAsync(toPhone, body, ct);
                _logger.LogInformation("Suspicious tx alert sent. To={Phone}, Tx={Tx}", MaskPhone(toPhone), txReference);
            }
            catch (Exception ex)
            {
                // fire-and-forget olmasa belə caller-ı bloklamamalıyıq
                _logger.LogError(ex, "Suspicious tx WhatsApp alert send failed. To={Phone}, Tx={Tx}",
                    MaskPhone(toPhone), txReference);
            }
        }

        private static string MaskPhone(string phone)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length < 4) return "***";
            return phone[..^4].Length > 0 ? new string('*', phone.Length - 4) + phone[^4..] : "***" + phone[^4..];
        }
    }
}
