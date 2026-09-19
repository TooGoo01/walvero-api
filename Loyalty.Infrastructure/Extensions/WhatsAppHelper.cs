using Microsoft.Extensions.Configuration;
using Twilio;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Loyalty.Infrastructure.Extensions
{
    /// <summary>
    /// Twilio WhatsApp Business API ilə mesaj göndərmə üçün static helper.
    /// Mirror of <see cref="Helper.SendSmsAsync"/> pattern — appsettings-dən credential oxuyur.
    /// </summary>
    public static class WhatsAppHelper
    {
        /// <summary>
        /// Twilio WhatsApp ilə mesaj göndərir. `Twilio:ContentTemplateSid` config-də varsa
        /// template ilə (24h window-dan kənarda işləyir), yoxsa free-form body ilə (24h içində).
        /// </summary>
        public static async Task<bool> SendWhatsAppAsync(string toPhone, string body, CancellationToken ct = default)
        {
            return await SendInternalAsync(toPhone, body, null, null, ct);
        }

        /// <summary>
        /// Template-li mesaj göndərir. Approved Content Template SID + variables istifadə edir.
        /// 24h conversation window-dan asılı deyil.
        /// </summary>
        /// <param name="contentVariables">Key-value dict — `{"1":"Walvero","2":"Səbail",...}` formatında JSON-laşır.</param>
        public static async Task<bool> SendWhatsAppTemplateAsync(
            string toPhone,
            string contentSid,
            IDictionary<string, string> contentVariables,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(contentSid))
                throw new InvalidOperationException("contentSid required for template send.");
            var json = System.Text.Json.JsonSerializer.Serialize(contentVariables);
            return await SendInternalAsync(toPhone, null, contentSid, json, ct);
        }

        /// <summary>
        /// OTP üçün: template mesajı göndərir və Twilio message SID-ni qaytarır
        /// (delivery statusunu sonradan yoxlamaq üçün). Yaradılma alınmasa exception.
        /// </summary>
        public static async Task<string?> SendTemplateReturningSidAsync(
            string toPhone,
            string contentSid,
            IDictionary<string, string> contentVariables,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(contentSid))
                throw new InvalidOperationException("contentSid required for template send.");
            var json = System.Text.Json.JsonSerializer.Serialize(contentVariables);
            return await SendInternalReturningSidAsync(toPhone, contentSid, json, ct);
        }

        /// <summary>
        /// Twilio-dan mesajın cari delivery statusunu çəkir (lowercase):
        /// queued/accepted/sending/sent/delivered/read/failed/undelivered.
        /// </summary>
        public static async Task<string?> FetchDeliveryStatusAsync(string messageSid, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(messageSid)) return null;
            try
            {
                var configuration = Helper_GetConfiguration();
                var accountSid = configuration["Twilio:AccountSid"];
                var authToken = configuration["Twilio:AuthToken"];
                if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken))
                    return null;
                TwilioClient.Init(accountSid, authToken);
                var msg = await MessageResource.FetchAsync(pathSid: messageSid);
                return msg?.Status?.ToString()?.ToLowerInvariant();
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string?> SendInternalReturningSidAsync(
            string toPhone, string contentSid, string contentVariables, CancellationToken ct)
        {
            var configuration = Helper_GetConfiguration();
            var accountSid = configuration["Twilio:AccountSid"];
            var authToken = configuration["Twilio:AuthToken"];
            var fromNumber = configuration["Twilio:WhatsAppFrom"];

            if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken))
                throw new InvalidOperationException("Twilio not configured (AccountSid/AuthToken).");
            if (string.IsNullOrWhiteSpace(fromNumber))
                throw new InvalidOperationException("Twilio:WhatsAppFrom not configured.");

            var toE164 = NormalizeToE164(toPhone);
            TwilioClient.Init(accountSid, authToken);

            var options = new Twilio.Rest.Api.V2010.Account.CreateMessageOptions(
                new PhoneNumber($"whatsapp:{toE164}"))
            {
                From = new PhoneNumber($"whatsapp:{fromNumber}"),
                ContentSid = contentSid,
                ContentVariables = contentVariables,
            };
            var msg = await MessageResource.CreateAsync(options);

            // Yaradılma anında error code varsa (məs. nömrə WhatsApp-da yoxdur) — uğursuz
            if (msg.ErrorCode.HasValue)
                throw new InvalidOperationException(
                    $"Twilio WhatsApp send failed (ErrorCode={msg.ErrorCode}, SID={msg.Sid})");

            return msg.Sid;
        }

        private static async Task<bool> SendInternalAsync(
            string toPhone, string? body, string? contentSid, string? contentVariables, CancellationToken ct)
        {
            var configuration = Helper_GetConfiguration();
            var accountSid = configuration["Twilio:AccountSid"];
            var authToken = configuration["Twilio:AuthToken"];
            var fromNumber = configuration["Twilio:WhatsAppFrom"]; // E.164 without "whatsapp:" prefix

            if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken))
                throw new InvalidOperationException("Twilio not configured (AccountSid/AuthToken).");
            if (string.IsNullOrWhiteSpace(fromNumber))
                throw new InvalidOperationException("Twilio:WhatsAppFrom not configured.");

            var toE164 = NormalizeToE164(toPhone);

            TwilioClient.Init(accountSid, authToken);
            try
            {
                MessageResource msg;
                if (!string.IsNullOrWhiteSpace(contentSid))
                {
                    var options = new Twilio.Rest.Api.V2010.Account.CreateMessageOptions(
                        new PhoneNumber($"whatsapp:{toE164}"))
                    {
                        From = new PhoneNumber($"whatsapp:{fromNumber}"),
                        ContentSid = contentSid,
                        ContentVariables = contentVariables,
                    };
                    msg = await MessageResource.CreateAsync(options);
                }
                else
                {
                    msg = await MessageResource.CreateAsync(
                        to: new PhoneNumber($"whatsapp:{toE164}"),
                        from: new PhoneNumber($"whatsapp:{fromNumber}"),
                        body: body);
                }

                if (msg.ErrorCode.HasValue)
                    throw new InvalidOperationException(
                        $"Twilio WhatsApp send failed (ErrorCode={msg.ErrorCode}, ErrorMessage={msg.ErrorMessage}, SID={msg.Sid})");

                return true;
            }
            catch (ApiException ex)
            {
                throw new InvalidOperationException(
                    $"Twilio WhatsApp API error (Code={ex.Code}, Message={ex.Message})", ex);
            }
        }

        /// <summary>Config-dən optional Template SID oxu (`Twilio:ContentTemplateSid`).</summary>
        public static string? GetConfiguredTemplateSid()
        {
            var c = Helper_GetConfiguration();
            var sid = c["Twilio:ContentTemplateSid"];
            return string.IsNullOrWhiteSpace(sid) ? null : sid;
        }

        private static string NormalizeToE164(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                throw new InvalidOperationException("Boş telefon nömrəsi.");

            var trimmed = phone.Trim();
            var digits = new string(trimmed.Where(char.IsDigit).ToArray());
            if (digits.Length == 0)
                throw new InvalidOperationException($"Telefon nömrəsində rəqəm yoxdur: {phone}");

            return trimmed.StartsWith("+") ? "+" + digits : "+" + digits;
        }

        // appsettings.json axtarışı SMS helper ilə eyni pattern; ortaq utility kimi adlandırılıb
        private static IConfiguration Helper_GetConfiguration()
        {
            var configurationManager = new ConfigurationManager();
            var basePath = Directory.GetCurrentDirectory();
            var webApiPath = Path.Combine(basePath, "..", "Loyalty.WebAPI");
            if (!Directory.Exists(webApiPath)) webApiPath = basePath;

            configurationManager.SetBasePath(webApiPath);
            configurationManager.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (!string.IsNullOrEmpty(env))
                configurationManager.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false);

            // Azure App Service env vars (Twilio:AccountSid və s.) — JSON-dan üstündür.
            configurationManager.AddEnvironmentVariables();

            return configurationManager;
        }
    }
}
