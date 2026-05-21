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
        /// Twilio WhatsApp ilə mesaj göndərir. Uğursuzlarda <c>InvalidOperationException</c> atır.
        /// </summary>
        public static async Task<bool> SendWhatsAppAsync(string toPhone, string body, CancellationToken ct = default)
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
                var msg = await MessageResource.CreateAsync(
                    to: new PhoneNumber($"whatsapp:{toE164}"),
                    from: new PhoneNumber($"whatsapp:{fromNumber}"),
                    body: body);

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
