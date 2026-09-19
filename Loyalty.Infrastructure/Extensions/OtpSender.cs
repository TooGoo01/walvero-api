using Microsoft.Extensions.Configuration;

namespace Loyalty.Infrastructure.Extensions
{
    /// <summary>
    /// OTP kanal göndəricisi: WhatsApp-first (yalnız approved Authentication template SID
    /// konfiqdə doludursa — freeform business-initiated mesaj çatmır), uğursuz/boş → SMS.
    /// </summary>
    public static class OtpSender
    {
        /// <returns>Faktiki istifadə olunan kanal: "whatsapp" | "sms"</returns>
        public static async Task<string> SendAsync(
            string phone,
            string code,
            string smsText,
            bool preferWhatsApp = true,
            CancellationToken ct = default)
        {
            if (preferWhatsApp)
            {
                var templateSid = GetOtpTemplateSid();
                if (!string.IsNullOrWhiteSpace(templateSid))
                {
                    try
                    {
                        var sid = await WhatsAppHelper.SendTemplateReturningSidAsync(
                            phone,
                            templateSid!,
                            new Dictionary<string, string> { ["1"] = code },
                            ct);

                        // Twilio yaratma anında yalnız "queued" qaytarır — çatıb-çatmadığını
                        // bilmək üçün delivery statusunu qısa müddət poll et. Çatıbsa whatsapp,
                        // uğursuz/ilişib qalıbsa SMS fallback.
                        if (!string.IsNullOrWhiteSpace(sid) &&
                            await DeliveredViaWhatsAppAsync(sid!, ct))
                            return "whatsapp";
                    }
                    catch
                    {
                        // WhatsApp cəhdi alınmadı → SMS fallback
                    }
                }
            }

            await Helper.SendSmsAsync(phone, smsText);
            return "sms";
        }

        // Success = WhatsApp serverlərinə/cihaza çatdı; failure/timeout → SMS lazımdır.
        private static readonly HashSet<string> _successStatuses =
            new(StringComparer.OrdinalIgnoreCase) { "delivered", "read", "sent" };
        private static readonly HashSet<string> _failureStatuses =
            new(StringComparer.OrdinalIgnoreCase) { "failed", "undelivered" };

        /// <summary>
        /// Mesaj statusunu ~8 saniyəyə qədər poll edir. delivered/read/sent → true;
        /// failed/undelivered → false; timeout-da hələ queued/accepted qalıbsa (ilişib) → false.
        /// </summary>
        private static async Task<bool> DeliveredViaWhatsAppAsync(string sid, CancellationToken ct)
        {
            const int maxAttempts = 6;
            for (int i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1300), ct);
                var status = await WhatsAppHelper.FetchDeliveryStatusAsync(sid, ct);
                if (status == null) continue;
                if (_successStatuses.Contains(status)) return true;
                if (_failureStatuses.Contains(status)) return false;
                // queued/accepted/sending/scheduled → hələ nəticə yoxdur, gözlə
            }
            // Timeout: hələ terminal statusa çatmayıb → təhlükəsizlik üçün SMS
            return false;
        }

        private static string? GetOtpTemplateSid()
        {
            try
            {
                var configurationManager = new ConfigurationManager();
                var basePath = Directory.GetCurrentDirectory();
                var webApiPath = Path.Combine(basePath, "..", "Loyalty.WebAPI");
                if (!Directory.Exists(webApiPath)) webApiPath = basePath;
                var config = configurationManager
                    .SetBasePath(webApiPath)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();
                var sid = config["Twilio:OtpContentTemplateSid"];
                return string.IsNullOrWhiteSpace(sid) ? null : sid;
            }
            catch
            {
                return null;
            }
        }
    }
}
