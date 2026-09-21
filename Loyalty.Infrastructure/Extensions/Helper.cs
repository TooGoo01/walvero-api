using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Twilio;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;

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
        public static async Task<bool> SendSmsAsync(string phone, string message, CancellationToken ct = default, string purpose = "INF")
        {
            // Telefon nömrəsindən rəqəmləri çıxar
            static string ExtractDigits(string p)
            {
                return new string(p.Where(char.IsDigit).ToArray());
            }

            var digits = ExtractDigits(phone);

            // Əgər nömrə 994 ilə başlayırsa, PoctGoyercini istifadə et.
            // purpose: OTP kodları "OTP" kateqoriyası (prioritet çatdırılma), digərləri "INF".
            if (digits.StartsWith("994"))
            {
                return await SendSmsViaPoctGoyerciniAsync(phone, message, ct, purpose);
            }

            // Əks halda Twilio istifadə et (purpose Twilio-da yoxdur — ignore)
            return await SendSmsViaTwilioAsync(phone, message, ct);
        }

        /// <summary>
        /// PG365 (Poct Goyercini) SMS API v1.0.4 ilə SMS göndərir.
        /// Endpoint: POST https://api.poctgoyercini.com/gateway/api/sms/v1/message/send
        /// </summary>
        private static async Task<bool> SendSmsViaPoctGoyerciniAsync(string phone, string message, CancellationToken ct = default, string purpose = "INF")
        {
            // 1) Telefonu beynəlxalq formata sal: 994XXXXXXXXX (ölkə kodu ilə)
            static string NormalizePhone(string p)
            {
                var digits = new string(p.Where(char.IsDigit).ToArray());
                if (digits.StartsWith("994")) return digits;
                if (digits.StartsWith("0")) digits = digits[1..];
                return "994" + digits;
            }



            const string publicKey = "740d51a4c88540cc";
            const string privateKey = "eyJhbGciOiJBMjU2S1ciLCJlbmMiOiJBMjU2Q0JDLUhTNTEyIiwidHlwIjoiSldUIiwiY3R5IjoiSldUIn0.wP3e4Vr--dPnq2Em_U0X5_EGXL8LEcp-shuWNss87vefhZMv-PyM0ec7GNgGqiwJk2kmWN4lOckB1e1S9r4FdtMd2Jpy3wZq.nSEJe5UTvZKDRv258Aj_Xw.mcCAmY4A3MMZSRaiTYlP6kMMra66McpI_j9NoNRSxv1c-xvVIi5k5G_sWTqP-Hqi4slxmB_cI61Vlb9BYzhli5TbU0izzkYQ63dNKZVpPFndACGlk_5iV8gSexGMnWAk1ZuHjN0czXBTWmqp5C1uMvs_A0Ma9eeP_4xUo56c09R0lix85zMxJ2TJosMTtryeYh4dFM1_n_NSZpNnQ85qOjnqbj7Etqv03tkMaeabvnri4zZNvqZ7UR_fP8kxjBQ_5plXj4r9Ls1llQTIO5RBZ6PpNW794wwytKuGrgkfI9EG7uKG2O33_8iXpdd25VCHD0R7iY17cwqcBcPmrrL2GwjeIRx7hHkfdFs7lt6_K4rPcwuDp0eSm4xqQU3jiv3z0r90et4m2k4ztK8AulwlJB06ijM06uWEuAbyJGAGIPQjYOxoZvX1uFDelRmX63D9QXGWFhEVtGspQ_g7PMQnUA.AovwPYVqP9v9W9Q2xI_FZjg4F5jQr1wCDc0OofS0eps";
            var receiver = NormalizePhone(phone);
            var endpoint = $"https://api.poctgoyercini.com/gateway/api/sms/v1/message/send?publicKey={publicKey}";

            // 2) JSON request body hazırla (Single Message – Multi-Recipient)
            var requestBody = new
            {
                Text = message,
                Purpose = string.IsNullOrWhiteSpace(purpose) ? "INF" : purpose,
                Options = new
                {
                    Originator = "Walvero",
                    Encoding = "LATIN",
                    SmsType = "SMS"
                },
                Receivers = new[]
                {
                    new { Receiver = receiver }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + privateKey);

            var resp = await http.PostAsync(endpoint, content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            // 3) Cavabı parse et: { "Status": 200, "Description": "", "Result": { ... } }
            int status;
            string? description;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                // Status həm int, həm string olaraq gələ bilər
                status = root.GetProperty("Status").ValueKind == JsonValueKind.Number
                    ? root.GetProperty("Status").GetInt32()
                    : int.TryParse(root.GetProperty("Status").GetString(), out var s) ? s : -1;
                description = root.TryGetProperty("Description", out var descProp) ? descProp.GetString() : null;
            }
            catch
            {
                throw new InvalidOperationException(
                    $"PG365 SMS cavab parse edilə bilmədi. HTTP {(int)resp.StatusCode}, Body: {body}");
            }

            if (status == 200)
                return true;

            throw new InvalidOperationException(
                $"PG365 SMS göndərmə alınmadı (Status={status}, Description={description}).");
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

            // Azure App Service env vars JSON-dan üstün — secret keys orada saxlanır
            configurationManager.AddEnvironmentVariables();

            return configurationManager;
        }
    }
}
