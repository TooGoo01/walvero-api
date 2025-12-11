using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Loyalty.Infrastructure.Extensions
{
    public static class Helper
    {
        /// <summary>
        /// PoctGoyercini HTTP API ilə SMS göndərir.
        /// Uğurda true qaytarır; əks halda InvalidOperationException atır.
        /// </summary>
        public static async Task<bool> SendSmsAsync(string phone, string message, CancellationToken ct = default)
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
                ["password"] = "kart12s",
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
    }
}
