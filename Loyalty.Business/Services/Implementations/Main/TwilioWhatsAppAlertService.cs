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

        public Task SendSuspiciousTxAlertAsync(
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
            return SendSuspiciousTxAlertAsync(new SuspiciousAlertContext
            {
                ToPhone = toPhone,
                TenantName = tenantName,
                BranchName = branchName,
                CustomerName = customerName,
                OperatorName = operatorName,
                EnteredAmount = enteredAmount,
                OcrAmount = ocrAmount,
                Cap = cap,
                SuspiciousReason = suspiciousReason,
                TxReference = txReference,
            }, ct);
        }

        public async Task SendSuspiciousTxAlertAsync(SuspiciousAlertContext c, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(c.ToPhone))
            {
                _logger.LogWarning("Suspicious tx alert skipped — no toPhone configured. Tx={Tx}", c.TxReference);
                return;
            }

            var reasonLabel = c.SuspiciousReason switch
            {
                "OcrMismatch" => "OCR məbləği uyğunsuz",
                "OcrUnreadable" => "OCR oxunmadı",
                "AmountExceedsCap" => "Məbləğ limiti aşıb",
                "SpendCountExceedsCap" => "Xərcləmə sayı limiti aşıb",
                "DuplicateReceiptId" => "Eyni çek təkrar istifadə edilib (dedupe)",
                "ObjectCodeMismatch" => "Obyekt Kodu uyğunsuz (başqa filial çeki?)",
                "ReceiptDateMismatch" => "Çek tarixi bu gün deyil",
                "TaxNumberMismatch" => "VÖEN uyğunsuz (başqa şirkət çeki?)",
                "QrMissing" => "QR kod yoxdur / oxunmadı",
                "QrVerificationFailed" => "QR doğrulaması uğursuz",
                "BalanceApplyFailed" => "Balans tətbiqi uğursuz",
                "OcrDownloadFailed" => "Çek şəkli endirilə bilmədi",
                _ => c.SuspiciousReason ?? "Naməlum",
            };

            var lines = new List<string>
            {
                $"⚠️ Şübhəli əməliyyat — {c.TenantName}",
                $"Filial: {c.BranchName ?? "—"}",
                $"Səbəb: {reasonLabel}",
                $"Müştəri: {c.CustomerName ?? "—"}",
                $"Operator: {c.OperatorName ?? "—"}",
                $"Daxil edilən məbləğ: {c.EnteredAmount:0.##} AZN",
            };
            if (c.OcrAmount.HasValue)
                lines.Add($"Çekdən oxunan (OCR): {c.OcrAmount.Value:0.##} AZN");
            if (c.Cap.HasValue && c.Cap.Value > 0)
                lines.Add($"Limit (cap): {c.Cap.Value:0.##} AZN");

            // Extended context — yalnız aktual sahələri göstər (boş sahələr atılır)
            if (c.ReceiptDate.HasValue)
                lines.Add($"Çekin tarixi: {c.ReceiptDate.Value:dd.MM.yyyy HH:mm}");
            if (!string.IsNullOrWhiteSpace(c.ExtractedObjectCode) || !string.IsNullOrWhiteSpace(c.ExpectedObjectCode))
                lines.Add($"Obyekt Kodu: tapılan='{c.ExtractedObjectCode ?? "—"}' gözlənilən='{c.ExpectedObjectCode ?? "—"}'");
            if (!string.IsNullOrWhiteSpace(c.ExtractedTaxNumber) || !string.IsNullOrWhiteSpace(c.ExpectedTaxNumber))
                lines.Add($"VÖEN: tapılan='{c.ExtractedTaxNumber ?? "—"}' gözlənilən='{c.ExpectedTaxNumber ?? "—"}'");
            if (!string.IsNullOrWhiteSpace(c.PhysicalReceiptId))
                lines.Add($"Fiskal İD: {c.PhysicalReceiptId}");
            if (!string.IsNullOrWhiteSpace(c.QrDocId))
                lines.Add($"E-kassa link: https://monitoring.e-kassa.gov.az/#/index?doc={c.QrDocId}");

            lines.Add($"Tx: {c.TxReference}");

            var body = string.Join("\n", lines);
            var templateSid = WhatsAppHelper.GetConfiguredTemplateSid();

            try
            {
                if (!string.IsNullOrWhiteSpace(templateSid))
                {
                    // Template path — 24h window-dan asılı deyil. Variables map:
                    // {{1}}=tenant {{2}}=branch {{3}}=reason {{4}}=customer
                    // {{5}}=operator {{6}}=amount {{7}}=tx
                    var variables = new Dictionary<string, string>
                    {
                        ["1"] = c.TenantName,
                        ["2"] = c.BranchName ?? "—",
                        ["3"] = reasonLabel,
                        ["4"] = c.CustomerName ?? "—",
                        ["5"] = c.OperatorName ?? "—",
                        ["6"] = $"{c.EnteredAmount:0.##}",
                        ["7"] = c.TxReference,
                    };
                    await WhatsAppHelper.SendWhatsAppTemplateAsync(c.ToPhone, templateSid, variables, ct);
                    _logger.LogInformation("Suspicious tx alert sent (template). To={Phone}, Tx={Tx}, Reason={Reason}, TemplateSid={Sid}",
                        MaskPhone(c.ToPhone), c.TxReference, c.SuspiciousReason, templateSid);
                }
                else
                {
                    // Free-form path — 24h window içində işləyir, mövcud davranış.
                    await WhatsAppHelper.SendWhatsAppAsync(c.ToPhone, body, ct);
                    _logger.LogInformation("Suspicious tx alert sent (free-form). To={Phone}, Tx={Tx}, Reason={Reason}",
                        MaskPhone(c.ToPhone), c.TxReference, c.SuspiciousReason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Suspicious tx WhatsApp alert send failed. To={Phone}, Tx={Tx}",
                    MaskPhone(c.ToPhone), c.TxReference);
            }
        }

        private static string MaskPhone(string phone)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length < 4) return "***";
            return phone[..^4].Length > 0 ? new string('*', phone.Length - 4) + phone[^4..] : "***" + phone[^4..];
        }
    }
}
