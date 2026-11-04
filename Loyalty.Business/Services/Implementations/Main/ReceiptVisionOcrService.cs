using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Çek şəklini Azure OpenAI vision (gpt-5-mini) ilə oxuyur, strukturlu JSON qaytarır.
    /// Chatbot ilə eyni AzureOpenAI config (endpoint normalize, api-version, gpt-5 params).
    /// Dəqiqlik üçün: json_schema strict cavab, 2000px JPEG + detail=high, AZ çek domen prompt-u,
    /// 429/5xx/timeout retry. Heç vaxt exception atmır — Ok=false (+Transient) qaytarır.
    /// </summary>
    public sealed class ReceiptVisionOcrService : IReceiptVisionOcr
    {
        private readonly IAzureOpenAiConfigProvider _aiConfig;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<ReceiptVisionOcrService> _logger;

        private const int MaxDim = 2000;          // kiçik rəqəmlər 1400-də əriyirdi; 2000 "detail=high" üçün optimal
        private const int JpegQuality = 90;
        private const int MaxAttempts = 3;        // 1 + 2 retry
        private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) };

        public ReceiptVisionOcrService(
            IAzureOpenAiConfigProvider aiConfig,
            IHttpClientFactory httpFactory,
            ILogger<ReceiptVisionOcrService> logger)
        {
            _aiConfig = aiConfig;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        public async Task<VisionReceiptFields> ReadAsync(byte[] imageBytes, VisionReceiptHints? hints = null, CancellationToken ct = default)
        {
            var failed = new VisionReceiptFields { Ok = false };

            var ai = await _aiConfig.GetAsync(ct);
            var endpoint = ai.Endpoint;
            var apiKey = ai.ApiKey;
            var deployment = ai.Deployment;
            var apiVersion = ai.ApiVersion ?? "2025-04-01-preview";
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deployment))
            {
                _logger.LogWarning("Receipt vision: AzureOpenAI not configured.");
                return failed;
            }
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var epUri))
                endpoint = $"{epUri.Scheme}://{epUri.Authority}";

            string dataUrl;
            try
            {
                var jpeg = await PrepareImageAsync(imageBytes, ct);
                dataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(jpeg);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Receipt vision: image prep failed.");
                return failed;
            }

            var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
            var client = _httpFactory.CreateClient(AzureOpenAiPlaceAssistantService.HttpClientName);

            // Effort konfiqurasiyadan (gpt-5.6 "minimal" qəbul etmir: none/low/...); yoxdursa köhnə default, "omit" → göndərmə.
            var reasoningEffort = ai.ReasoningEffort ?? "minimal";
            bool useSchema = true;

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var body = BuildBody(dataUrl, hints, reasoningEffort, useSchema);
                int status;
                string json;
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
                    };
                    req.Headers.Add("api-key", apiKey);

                    using var resp = await client.SendAsync(req, ct);
                    status = (int)resp.StatusCode;
                    json = await resp.Content.ReadAsStringAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
                {
                    // Şəbəkə / timeout — tranzient, retry.
                    _logger.LogWarning(ex, "Receipt vision: network error (attempt {Attempt}/{Max}).", attempt + 1, MaxAttempts);
                    if (await DelayBeforeRetryAsync(attempt, ct)) continue;
                    failed.Transient = true;
                    return failed;
                }

                if (status == 429 || status >= 500)
                {
                    _logger.LogWarning("Receipt vision: AzureOpenAI {Status} (attempt {Attempt}/{Max}): {Body}",
                        status, attempt + 1, MaxAttempts, Trunc(json, 300));
                    if (await DelayBeforeRetryAsync(attempt, ct)) continue;
                    failed.Transient = true;
                    return failed;
                }

                if (status == 400 && useSchema && json.Contains("response_format", StringComparison.OrdinalIgnoreCase))
                {
                    // Model/API json_schema dəstəkləmir — bir dəfə onsuz təkrar (prompt-da JSON tələbi qalır).
                    _logger.LogWarning("Receipt vision: response_format rejected, retrying without schema: {Body}", Trunc(json, 300));
                    useSchema = false;
                    continue;
                }

                if (status is < 200 or >= 300)
                {
                    _logger.LogError("Receipt vision AzureOpenAI error {Status}: {Body}", status, Trunc(json, 300));
                    return failed;
                }

                string? content;
                try
                {
                    content = JsonNode.Parse(json)?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Receipt vision: response envelope parse failed.");
                    return failed;
                }
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("Receipt vision: empty content. Envelope={Body}", Trunc(json, 300));
                    return failed;
                }

                var parsed = ReceiptVisionJsonParser.Parse(content);
                if (parsed is null)
                {
                    _logger.LogWarning("Receipt vision: content is not JSON: {Content}", Trunc(content, 300));
                    return failed;
                }

                parsed.Ok = true;
                _logger.LogInformation("Receipt vision parsed: {Json}", Trunc(parsed.RawJson ?? string.Empty, 1000));
                return parsed;
            }

            failed.Transient = true;
            return failed;
        }

        private async Task<bool> DelayBeforeRetryAsync(int attempt, CancellationToken ct)
        {
            if (attempt >= MaxAttempts - 1) return false;
            var delay = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
            await Task.Delay(delay, ct);
            return true;
        }

        private static JsonObject BuildBody(string dataUrl, VisionReceiptHints? hints, string reasoningEffort, bool useSchema)
        {
            var sys =
                "You are a precise OCR engine for cash-register receipts from Azerbaijan (Azerbaijani, sometimes Russian or English). " +
                "Thermal prints are often faded, skewed, or photographed with shadows. Diacritics are frequently missing or garbled " +
                "(ə→e, ö→o, ü→u, ş→s, ç→c, ğ→g, İ/ı→I/i), so treat such variants as the same word. " +
                "Return ONLY a JSON object matching the required schema. Never invent values: if a field is not clearly readable, return null.";

            var sb = new StringBuilder();
            sb.AppendLine("Extract these fields from the receipt image:");
            sb.Append("- amount: the FINAL amount actually paid (grand total). Typical labels: \"Yekun məbləğ\", \"YEKUN MEBLEG\", \"Cəmi\", \"Ödəniləcək\", \"Ödənildi\", \"Toplam\", \"Итого\", \"Total\"");
            AppendHint(sb, hints?.AmountLabels);
            sb.AppendLine(". It is NOT the subtotal (\"Ara cəm\"), NOT cash tendered (\"Nağd\", \"Verilən\", \"Alınan\"), NOT the change (\"Qalıq\", \"Qaytarılan\"), NOT a discount, VAT or service-charge line on its own. If a service charge (\"Xidmət haqqı\") is already included in the total, return the total that includes it. Plain number with a dot decimal separator.");
            sb.Append("- taxNumber: the seller's tax id VÖEN / VOEN / \"Vergi nömrəsi\" / ИНН (usually 10 digits), digits only");
            AppendHint(sb, hints?.TaxNumberLabels);
            sb.AppendLine(".");
            sb.Append("- objectCode: value after \"Obyektin kodu\" / \"Obyekt kodu\"");
            AppendHint(sb, hints?.ObjectCodeLabels);
            sb.AppendLine(".");
            sb.Append("- receiptId: fiscal receipt id (\"Fiskal ID\", \"Fiskal İD\", \"Fiscal ID\", \"Çek nömrəsi\")");
            AppendHint(sb, hints?.ReceiptIdLabels);
            sb.AppendLine(".");
            sb.AppendLine("- dateIso: the sale date printed on the receipt as YYYY-MM-DD (convert dd.mm.yyyy, dd/mm/yyyy, dd-mm-yy). Ignore other dates (e.g. expiry, registration).");
            sb.AppendLine("- orderNo: order / check number (\"Sifariş №\", \"Sifariş #\", \"Çek №\", \"Заказ №\"), digits only.");
            sb.AppendLine("- isFiscal: true if it is a fiscal e-kassa receipt (has VÖEN, Fiskal ID, NMQ/NKA, \"e-kassa\", \"fiskal\", or a QR to monitoring.e-kassa.gov.az); false for non-fiscal slips such as \"Ödəniş çeki\" without fiscal markers.");
            sb.AppendLine("- merchantName: the business / restaurant / branch name printed in the header (top lines), exactly as printed.");
            sb.AppendLine("- rawText: full transcription of all readable text, one receipt line per line, in reading order. Keep labels and numbers exactly as printed.");
            sb.Append("Use null for any field that is absent or unreadable. Output JSON only.");

            var userContent = new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = sb.ToString() },
                new JsonObject
                {
                    ["type"] = "image_url",
                    ["image_url"] = new JsonObject { ["url"] = dataUrl, ["detail"] = "high" }
                }
            };

            var body = new JsonObject
            {
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "system", ["content"] = sys },
                    new JsonObject { ["role"] = "user", ["content"] = userContent }
                },
                ["max_completion_tokens"] = 2000
            };
            if (!string.Equals(reasoningEffort, "omit", StringComparison.OrdinalIgnoreCase))
                body["reasoning_effort"] = reasoningEffort;
            if (useSchema)
                body["response_format"] = BuildResponseFormat();
            return body;
        }

        private static void AppendHint(StringBuilder sb, string? labels)
        {
            if (string.IsNullOrWhiteSpace(labels)) return;
            var parts = labels.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).Distinct().ToList();
            if (parts.Count == 0) return;
            sb.Append("; configured labels to also look for: ");
            sb.Append(string.Join(", ", parts.Select(p => $"\"{p}\"")));
        }

        private static JsonObject BuildResponseFormat()
        {
            static JsonObject Nullable(string type) => new() { ["type"] = new JsonArray(type, "null") };

            var props = new JsonObject
            {
                ["amount"] = Nullable("number"),
                ["taxNumber"] = Nullable("string"),
                ["objectCode"] = Nullable("string"),
                ["receiptId"] = Nullable("string"),
                ["dateIso"] = Nullable("string"),
                ["orderNo"] = Nullable("string"),
                ["isFiscal"] = new JsonObject { ["type"] = "boolean" },
                ["merchantName"] = Nullable("string"),
                ["rawText"] = Nullable("string"),
            };
            var required = new JsonArray("amount", "taxNumber", "objectCode", "receiptId", "dateIso", "orderNo", "isFiscal", "merchantName", "rawText");

            return new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = "receipt",
                    ["strict"] = true,
                    ["schema"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = props,
                        ["required"] = required,
                        ["additionalProperties"] = false
                    }
                }
            };
        }

        private static string Trunc(string s, int max) => s.Length > max ? s.Substring(0, max) : s;

        /// <summary>EXIF fırlanma düzəlt, max tərəf 2000px, JPEG q90 (PNG-dən 3–5x kiçik payload).</summary>
        private static async Task<byte[]> PrepareImageAsync(byte[] input, CancellationToken ct)
        {
            using var image = Image.Load(input);
            image.Mutate(x => x.AutoOrient());
            var maxDim = Math.Max(image.Width, image.Height);
            if (maxDim > MaxDim)
            {
                var scale = (float)MaxDim / maxDim;
                image.Mutate(x => x.Resize((int)(image.Width * scale), (int)(image.Height * scale)));
            }
            using var ms = new MemoryStream();
            await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = JpegQuality }, ct);
            return ms.ToArray();
        }
    }

    /// <summary>Modelin JSON cavabını VisionReceiptFields-ə çevirir (lenient: ```json fence, string ədəd, vergül decimal).</summary>
    public static class ReceiptVisionJsonParser
    {
        /// <summary>JSON tapılmadısa null; tapıldısa Ok=false ilə doldurulmuş obyekt (Ok-u çağıran təyin edir).</summary>
        public static VisionReceiptFields? Parse(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;
            var s = content.Trim();
            var start = s.IndexOf('{');
            var end = s.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            s = s.Substring(start, end - start + 1);

            JsonNode? node;
            try { node = JsonNode.Parse(s); }
            catch { return null; }
            if (node is null) return null;

            var r = new VisionReceiptFields { RawJson = s };
            r.Amount = ParseDecimal(node["amount"]);
            r.TaxNumber = Str(node["taxNumber"]);
            r.ObjectCode = Str(node["objectCode"]);
            r.ReceiptId = Str(node["receiptId"]);
            r.OrderNo = Str(node["orderNo"]);
            r.MerchantName = Str(node["merchantName"]);
            r.RawText = Str(node["rawText"]);
            r.IsFiscal = ParseBool(node["isFiscal"]);
            r.Date = ParseDate(Str(node["dateIso"]));
            return r;
        }

        private static bool ParseBool(JsonNode? n)
        {
            if (n is null) return false;
            try { return n.GetValue<bool>(); } catch { }
            try
            {
                var s = n.GetValue<string>();
                return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>Tarixi 12:00 UTC kimi qaytarır ki, hər saat qurşağında eyni təqvim gününə düşsün.</summary>
        private static DateTime? ParseDate(string? iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return null;
            var t = iso.Trim();
            if (t.Length > 10) t = t.Substring(0, 10); // "2026-11-04T18:30" → "2026-11-04"
            if (DateTime.TryParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                || DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
            {
                if (d.Year < 2000 || d.Year > 2100) return null;
                return new DateTime(d.Year, d.Month, d.Day, 12, 0, 0, DateTimeKind.Utc);
            }
            return null;
        }

        private static string? Str(JsonNode? n)
        {
            if (n is null) return null;
            try
            {
                var v = n.GetValue<string?>();
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }
            catch
            {
                // Ədəd gəlibsə (məs. orderNo: 264) string-ə çevir.
                try { return n.ToJsonString().Trim('"'); } catch { return null; }
            }
        }

        private static decimal? ParseDecimal(JsonNode? n)
        {
            if (n is null) return null;
            try { return n.GetValue<decimal>(); } catch { }
            try
            {
                var s = n.GetValue<string>()?.Replace(',', '.').Replace(" ", "").Trim();
                if (!string.IsNullOrWhiteSpace(s) &&
                    decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
                    return v;
            }
            catch { }
            return null;
        }
    }
}
