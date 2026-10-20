using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Çek şəklini Azure OpenAI vision (gpt-5-mini) ilə oxuyur, struktur JSON qaytarır.
    /// Chatbot ilə eyni AzureOpenAI config (endpoint normalize, api-version, gpt-5 params).
    /// </summary>
    public sealed class ReceiptVisionOcrService : IReceiptVisionOcr
    {
        private readonly IAzureOpenAiConfigProvider _aiConfig;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<ReceiptVisionOcrService> _logger;

        private const int MaxDim = 1400; // token/cost azaltmaq üçün kiçilt

        public ReceiptVisionOcrService(
            IAzureOpenAiConfigProvider aiConfig,
            IHttpClientFactory httpFactory,
            ILogger<ReceiptVisionOcrService> logger)
        {
            _aiConfig = aiConfig;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        public async Task<VisionReceiptFields> ReadAsync(byte[] imageBytes, CancellationToken ct = default)
        {
            var result = new VisionReceiptFields { Ok = false };

            var ai = await _aiConfig.GetAsync(ct);
            var endpoint = ai.Endpoint;
            var apiKey = ai.ApiKey;
            var deployment = ai.Deployment;
            var apiVersion = ai.ApiVersion ?? "2025-04-01-preview";
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deployment))
            {
                _logger.LogWarning("Receipt vision: AzureOpenAI not configured.");
                return result;
            }
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var epUri))
                endpoint = $"{epUri.Scheme}://{epUri.Authority}";

            string dataUrl;
            try
            {
                var png = await DownscalePngAsync(imageBytes, ct);
                dataUrl = "data:image/png;base64," + Convert.ToBase64String(png);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Receipt vision: image prep failed.");
                return result;
            }

            var sys = "You extract structured data from a cash-register/payment receipt image. "
                    + "Return STRICT JSON only, no prose.";
            // Vision content: text instruction + image_url.
            var userContent = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] =
                        "Read this receipt and return ONLY this JSON object:\n" +
                        "{\"amount\":number|null, \"taxNumber\":string|null, \"objectCode\":string|null, " +
                        "\"receiptId\":string|null, \"dateIso\":string|null, \"orderNo\":string|null, \"isFiscal\":boolean}\n" +
                        "amount = the TOTAL paid amount (grand total / cəmi / yekun / ödəniləcək), as a number with a dot decimal.\n" +
                        "taxNumber = VÖEN/VOEN if present. objectCode = object code (obyekt kodu) if present.\n" +
                        "receiptId = fiscal/receipt id if present. dateIso = receipt date as YYYY-MM-DD if present.\n" +
                        "orderNo = order number (Sifariş №) if present. isFiscal = true if it is a fiscal e-kassa receipt.\n" +
                        "Use null when a field is absent. Output JSON only."
                },
                new JsonObject
                {
                    ["type"] = "image_url",
                    ["image_url"] = new JsonObject { ["url"] = dataUrl }
                }
            };

            var body = new JsonObject
            {
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "system", ["content"] = sys },
                    new JsonObject { ["role"] = "user", ["content"] = userContent }
                },
                ["max_completion_tokens"] = 800
            };
            // Effort konfiqurasiyadan (gpt-5.6 "minimal" qəbul etmir: none/low/...); yoxdursa köhnə default, "omit" → göndərmə.
            var reasoningEffort = ai.ReasoningEffort ?? "minimal";
            if (!string.Equals(reasoningEffort, "omit", StringComparison.OrdinalIgnoreCase))
                body["reasoning_effort"] = reasoningEffort;

            var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
            var client = _httpFactory.CreateClient(AzureOpenAiPlaceAssistantService.HttpClientName);

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
                };
                req.Headers.Add("api-key", apiKey);

                using var resp = await client.SendAsync(req, ct);
                var json = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("Receipt vision AzureOpenAI error {Status}: {Body}", (int)resp.StatusCode,
                        json.Length > 300 ? json.Substring(0, 300) : json);
                    return result;
                }

                var content = JsonNode.Parse(json)?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(content)) return result;

                Parse(content, result);
                result.Ok = true;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Receipt vision call failed.");
                return result;
            }
        }

        private static void Parse(string content, VisionReceiptFields r)
        {
            // Modelin ```json ...``` çıxarışını təmizlə.
            var s = content.Trim();
            var start = s.IndexOf('{');
            var end = s.LastIndexOf('}');
            if (start < 0 || end <= start) return;
            s = s.Substring(start, end - start + 1);

            JsonNode? node;
            try { node = JsonNode.Parse(s); }
            catch { return; }
            if (node is null) return;

            r.Amount = ParseDecimal(node["amount"]);
            r.TaxNumber = Str(node["taxNumber"]);
            r.ObjectCode = Str(node["objectCode"]);
            r.ReceiptId = Str(node["receiptId"]);
            r.OrderNo = Str(node["orderNo"]);
            r.IsFiscal = node["isFiscal"]?.GetValue<bool?>() ?? false;

            var dateIso = Str(node["dateIso"]);
            if (!string.IsNullOrWhiteSpace(dateIso) &&
                DateTime.TryParse(dateIso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                r.Date = d;
        }

        private static string? Str(JsonNode? n)
        {
            if (n is null) return null;
            try
            {
                var v = n.GetValue<string?>();
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }
            catch { return null; }
        }

        private static decimal? ParseDecimal(JsonNode? n)
        {
            if (n is null) return null;
            try
            {
                if (n.GetValue<object?>() is null) return null;
            }
            catch { }
            try { return n.GetValue<decimal>(); } catch { }
            try
            {
                var s = n.GetValue<string>()?.Replace(',', '.').Trim();
                if (!string.IsNullOrWhiteSpace(s) &&
                    decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
                    return v;
            }
            catch { }
            return null;
        }

        private static async Task<byte[]> DownscalePngAsync(byte[] input, CancellationToken ct)
        {
            using var image = Image.Load(input);
            var maxDim = Math.Max(image.Width, image.Height);
            if (maxDim > MaxDim)
            {
                var scale = (float)MaxDim / maxDim;
                image.Mutate(x => x.Resize((int)(image.Width * scale), (int)(image.Height * scale)));
            }
            using var ms = new MemoryStream();
            await image.SaveAsPngAsync(ms, ct);
            return ms.ToArray();
        }

    }
}
