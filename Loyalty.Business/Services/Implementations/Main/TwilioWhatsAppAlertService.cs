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
            string? customerName,
            decimal amount,
            decimal cap,
            string? operatorId,
            string txReference,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(toPhone))
            {
                _logger.LogWarning("Suspicious tx alert skipped — no toPhone configured. Tx={Tx}", txReference);
                return;
            }

            var body =
                $"⚠️ {tenantName} — şübhəli əməliyyat\n" +
                $"Müştəri: {customerName ?? "—"}\n" +
                $"Məbləğ: {amount:0.##} AZN (cap: {cap:0.##})\n" +
                $"Operator: {operatorId ?? "—"}\n" +
                $"Tx: {txReference}";

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
