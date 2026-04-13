using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Exceptions;

namespace Loyalty.Infrastructure.Extensions
{
    public static class Helper
    {
        /// <summary>
        /// Telefon nömrəsinə görə SMS göndərir:
        /// - Əgər nömrə 994 ilə başlayırsa → PoctGoyercini ilə göndərir
        /// - Əks halda → Twilio ilə göndərir
        /// Uğurda true qaytarır; əks halda InvalidOperationException atır.
        /// </summary>
        public static async Task<bool> SendSmsAsync(string phone, string message, CancellationToken ct = default)
        {
            // Telefon nömrəsindən rəqəmləri çıxar
            static string ExtractDigits(string p)
            {
                return new string(p.Where(char.IsDigit).ToArray());
            }

            var digits = ExtractDigits(phone);

            // Əgər nömrə 994 ilə başlayırsa, PoctGoyercini istifadə et
            if (digits.StartsWith("994"))
            {
                return await SendSmsViaPoctGoyerciniAsync(phone, message, ct);
            }

            // Əks halda Twilio istifadə et
            return await SendSmsViaTwilioAsync(phone, message, ct);
        }

        /// <summary>
        /// PoctGoyercini HTTP API ilə SMS göndərir.
        /// </summary>
        private static async Task<bool> SendSmsViaPoctGoyerciniAsync(string phone, string message, CancellationToken ct = default)
        {
            // 1) Telefonu sənəd formatına sal: 50xxxxxxx (başda +994/0/boşluq YOX)  ← sənəd
            static string NormalizePhone(string p)
            {
                var digits = new string(p.Where(char.IsDigit).ToArray());
                if (digits.StartsWith("994")) digits = digits[3..];
                if (digits.StartsWith("0")) digits = digits[1..];
                return digits;
            }

            var normalizedGsm = NormalizePhone(phone);

            // 2) Endpoint və parametrlər (HTTPS + POST + URL-encode)  ← sənəd
            const string endpoint = "https://www.poctgoyercini.com/api_http/sendsms.asp";
            var form = new Dictionary<string, string>
            {
                ["user"] = "kart12_servis",
                ["password"] = "TooGoo2025Lime",
                ["gsm"] = normalizedGsm,
                ["text"] = message, // FormUrlEncodedContent özü URL-encode edir
            };

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            using var content = new FormUrlEncodedContent(form);

            var resp = await http.PostAsync(endpoint, content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            // 3) Cavabı parse et: "errno=0&errtext=&message_id=...&charge=1"  ← sənəd
            var map = body.Split('&', StringSplitOptions.RemoveEmptyEntries)
                          .Select(p => p.Split('=', 2))
                          .Where(a => a.Length == 2)
                          .ToDictionary(a => a[0], a => Uri.UnescapeDataString(a[1]));

            int errno = map.TryGetValue("errno", out var eStr) && int.TryParse(eStr, out var eVal) ? eVal : -9999;
            map.TryGetValue("errtext", out var errtext);
            map.TryGetValue("message_id", out var messageId);
            // charge opsionaldır
            _ = map.TryGetValue("charge", out var chargeStr);

            if (errno == 0)
                return true;

            // Daha aydın diaqnostika üçün xətanı ataq
            throw new InvalidOperationException(
                $"SMS göndərmə alınmadı (errno={errno}, errtext={errtext}, message_id={messageId}).");
        }

        /// <summary>
        /// Twilio API ilə SMS göndərir.
        /// </summary>
        private static async Task<bool> SendSmsViaTwilioAsync(string phone, string message, CancellationToken ct = default)
        {
            // Twilio konfiqurasiyasını oxu
            var configuration = GetConfiguration();
            var accountSid = configuration["Twilio:AccountSid"];
            var authToken = configuration["Twilio:AuthToken"];

            if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken))
            {
                throw new InvalidOperationException(
                    "Twilio is not configured. Please set Twilio:AccountSid and Twilio:AuthToken in appsettings.json");
            }

            // Telefon nömrəsindən rəqəmləri çıxar və 44 ilə başlayıb-başlamadığını yoxla
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            var fromNumber = digits.StartsWith("44") 
                ? configuration["Twilio:FromStr"] 
                : configuration["Twilio:From"];

            if (string.IsNullOrWhiteSpace(fromNumber))
            {
                var configKey = digits.StartsWith("44") ? "Twilio:FromStr" : "Twilio:From";
                throw new InvalidOperationException(
                    $"{configKey} is not configured. Please set {configKey} in appsettings.json (E.164 format or Sender ID)");
            }

            // Telefon nömrəsini E.164 formatına çevir
            var toNumber = FormatToE164(phone);
            if (!IsValidE164(toNumber))
            {
                throw new InvalidOperationException(
                    $"Invalid phone number format: {phone}. Must be in E.164 format (e.g., +1234567890)");
            }

            // Twilio client-i initialize et
            TwilioClient.Init(accountSid, authToken);

            try
            {
                var twilioMessage = await MessageResource.CreateAsync(
                    to: toNumber,
                    from: fromNumber,
                    body: message
                );

                // Uğurlu status yoxla
                if (twilioMessage.ErrorCode.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Twilio SMS göndərmə alınmadı (ErrorCode={twilioMessage.ErrorCode}, ErrorMessage={twilioMessage.ErrorMessage}, SID={twilioMessage.Sid})");
                }

                return true;
            }
            catch (ApiException ex)
            {
                throw new InvalidOperationException(
                    $"Twilio API xətası (Code={ex.Code}, Message={ex.Message})", ex);
            }
        }

        /// <summary>
        /// Telefon nömrəsini E.164 formatına çevirir (+ ilə başlayan format).
        /// </summary>
        private static string FormatToE164(string phone)
        {
            // Rəqəmləri çıxar
            var digits = new string(phone.Where(char.IsDigit).ToArray());

            if (string.IsNullOrEmpty(digits))
                return phone; // Orijinal nömrəni qaytar

            // Əgər artıq + ilə başlayırsa, olduğu kimi qaytar
            if (phone.TrimStart().StartsWith("+"))
            {
                return "+" + digits;
            }

            // Əks halda + əlavə et
            return "+" + digits;
        }

        /// <summary>
        /// E.164 formatını yoxlayır.
        /// </summary>
        private static bool IsValidE164(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            // E.164 format: + followed by 1-15 digits
            return Regex.IsMatch(phoneNumber, @"^\+[1-9]\d{1,14}$");
        }

        /// <summary>
        /// appsettings.json-dan konfiqurasiya oxuyur.
        /// </summary>
        private static IConfiguration GetConfiguration()
        {
            var configurationManager = new ConfigurationManager();
            
            // WebAPI layihəsinin appsettings.json faylını tap
            var basePath = Directory.GetCurrentDirectory();
            var webApiPath = Path.Combine(basePath, "..", "Loyalty.WebAPI");
            
            // Əgər WebAPI path mövcud deyilsə, cari directory-də yoxla
            if (!Directory.Exists(webApiPath))
            {
                webApiPath = basePath;
            }

            configurationManager.SetBasePath(webApiPath);
            configurationManager.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            
            // Environment-specific appsettings faylını da əlavə et (əgər varsa)
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (!string.IsNullOrEmpty(env))
            {
                configurationManager.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false);
            }

            return configurationManager;
        }
    }
}
