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
                        var ok = await WhatsAppHelper.SendWhatsAppTemplateAsync(
                            phone,
                            templateSid!,
                            new Dictionary<string, string> { ["1"] = code },
                            ct);
                        if (ok) return "whatsapp";
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
