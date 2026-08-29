using Loyalty.Business.Dtos.Chatbot;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Azure OpenAI (chat completions + function calling) üzərində chatbot beyni.
    /// Raw HTTP — əlavə NuGet asılılığı yoxdur. Provider dəyişmək = yalnız bu faylı əvəz etmək.
    /// </summary>
    public class AzureOpenAiPlaceAssistantService : IPlaceAssistantService
    {
        public const string HttpClientName = "azure-openai";

        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly IPlaceSearchService _placeSearch;
        private readonly IMenuSearchService _menuSearch;
        private readonly ICustomerAssistantTools _customerTools;
        private readonly ILogger<AzureOpenAiPlaceAssistantService> _logger;

        private const int MaxToolIterations = 5;

        public AzureOpenAiPlaceAssistantService(
            IHttpClientFactory httpFactory,
            IConfiguration config,
            IPlaceSearchService placeSearch,
            IMenuSearchService menuSearch,
            ICustomerAssistantTools customerTools,
            ILogger<AzureOpenAiPlaceAssistantService> logger)
        {
            _menuSearch = menuSearch;
            _httpFactory = httpFactory;
            _config = config;
            _placeSearch = placeSearch;
            _customerTools = customerTools;
            _logger = logger;
        }

        public async Task<AssistantReply> AskAsync(ChatbotAskRequest request, CancellationToken ct = default)
        {
            // Həm `AzureOpenAI__X` (hierarxiya), həm flat `AZURE_OPENAI_X` adlarını dəstəklə.
            // DİQQƏT: appsettings.json boş placeholder ("") saxlayır → `??` boş string-də
            // keçmir. Ona görə boş/whitespace-i də "yoxdur" sayan PickConfig istifadə olunur.
            var endpoint = PickConfig(_config, "AzureOpenAI:Endpoint", "AZURE_OPENAI_ENDPOINT");
            var apiKey = PickConfig(_config, "AzureOpenAI:ApiKey", "AZURE_OPENAI_API_KEY");
            var deployment = PickConfig(_config, "AzureOpenAI:Deployment", "AZURE_OPENAI_DEPLOYMENT", "AZURE_OPENAI_DEPLOYMENT_NAME");
            var apiVersion = PickConfig(_config, "AzureOpenAI:ApiVersion", "AZURE_OPENAI_API_VERSION") ?? "2024-08-01-preview";

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deployment))
            {
                _logger.LogWarning("AzureOpenAI not configured; chatbot disabled.");
                return new AssistantReply { Reply = "AI köməkçi hazırda əlçatan deyil." };
            }

            bool isAuth = _customerTools.IsAuthenticated;

            var messages = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = BuildSystemPrompt(isAuth) }
            };
            foreach (var m in request.Messages ?? new List<ChatMessageDto>())
            {
                var role = string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
                messages.Add(new JsonObject { ["role"] = role, ["content"] = m.Content ?? string.Empty });
            }

            var tools = BuildTools(isAuth);
            var collectedPlaces = new List<PlaceResultDto>();

            var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
            var client = _httpFactory.CreateClient(HttpClientName);

            for (int iter = 0; iter < MaxToolIterations; iter++)
            {
                var body = new JsonObject
                {
                    ["messages"] = messages.DeepClone(),
                    ["tools"] = tools.DeepClone(),
                    ["tool_choice"] = "auto",
                    ["temperature"] = 0.2,
                    ["max_tokens"] = 900
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
                };
                req.Headers.Add("api-key", apiKey);

                using var resp = await client.SendAsync(req, ct);
                var json = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("AzureOpenAI error {Status}: {Body}", (int)resp.StatusCode, json);
                    return new AssistantReply { Reply = "AI köməkçidə müvəqqəti xəta oldu, bir azdan yenidən yoxlayın." };
                }

                JsonNode? root = JsonNode.Parse(json);
                var message = root?["choices"]?[0]?["message"]?.AsObject();
                if (message is null)
                    return new AssistantReply { Reply = "Cavab alına bilmədi." };

                var toolCalls = message["tool_calls"]?.AsArray();
                if (toolCalls is null || toolCalls.Count == 0)
                {
                    var content = message["content"]?.GetValue<string>() ?? string.Empty;
                    return new AssistantReply { Reply = content, Places = collectedPlaces };
                }

                // Assistant tool-call mesajını olduğu kimi geri əlavə et.
                messages.Add(message.DeepClone());

                foreach (var tcNode in toolCalls)
                {
                    var tc = tcNode!.AsObject();
                    var id = tc["id"]?.GetValue<string>() ?? "";
                    var fn = tc["function"]?.AsObject();
                    var name = fn?["name"]?.GetValue<string>() ?? "";
                    var argsRaw = fn?["arguments"]?.GetValue<string>() ?? "{}";

                    string resultJson = await DispatchToolAsync(name, argsRaw, request, isAuth, collectedPlaces, ct);

                    messages.Add(new JsonObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = id,
                        ["content"] = resultJson
                    });
                }
            }

            // Tool loop limitə çatdı — sonuncu keçidi mətn üçün istifadə et.
            return new AssistantReply
            {
                Reply = "Sorğunu tam emal edə bilmədim, zəhmət olmasa daha dəqiq yazın.",
                Places = collectedPlaces
            };
        }

        private async Task<string> DispatchToolAsync(
            string name, string argsRaw, ChatbotAskRequest request, bool isAuth,
            List<PlaceResultDto> collectedPlaces, CancellationToken ct)
        {
            try
            {
                JsonNode args = JsonNode.Parse(string.IsNullOrWhiteSpace(argsRaw) ? "{}" : argsRaw) ?? new JsonObject();

                switch (name)
                {
                    case "search_places":
                    {
                        var query = new PlaceSearchQuery
                        {
                            Keywords = args["keywords"]?.AsArray()?
                                .Select(n => GetString(n)).Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToArray()
                                ?? Array.Empty<string>(),
                            CategoryName = GetString(args["categoryName"]),
                            City = GetString(args["city"]) ?? request.City,
                            RadiusKm = GetDouble(args["radiusKm"]),
                            Lat = request.Lat, // geo request-dən — modeldən istəmirik
                            Lng = request.Lng,
                            Take = 8
                        };
                        var places = await _placeSearch.SearchAsync(query, ct);

                        foreach (var p in places)
                            if (!collectedPlaces.Any(x => x.BranchId == p.BranchId))
                                collectedPlaces.Add(p);

                        return Serialize(places.Select(p => new
                        {
                            p.TenantId, p.BranchId, p.Name, p.CategoryName, p.Address,
                            p.DistanceKm, p.Keywords, p.Phone,
                            menuInApp = p.Menu.InApp, p.Menu.ExternalMenuUrl
                        }));
                    }

                    case "search_menu_items":
                    {
                        var mq = new MenuItemSearchQuery
                        {
                            Keywords = args["keywords"]?.AsArray()?
                                .Select(n => GetString(n)).Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToArray()
                                ?? Array.Empty<string>(),
                            RadiusKm = GetDouble(args["radiusKm"]),
                            Lat = request.Lat,
                            Lng = request.Lng,
                            Take = 8
                        };
                        var hits = await _menuSearch.SearchAsync(mq, ct);

                        foreach (var h in hits)
                            if (!collectedPlaces.Any(x => x.BranchId == h.Place.BranchId))
                                collectedPlaces.Add(h.Place);

                        return Serialize(hits.Select(h => new
                        {
                            h.Place.TenantId, h.Place.BranchId, place = h.Place.Name,
                            h.Place.Address, h.Place.DistanceKm,
                            dish = h.DishName, h.Description, h.Price, h.CurrencySign,
                            menuInApp = h.Place.Menu.InApp
                        }));
                    }

                    case "get_my_cards":
                    case "get_my_balance":
                    {
                        if (!isAuth) return "{\"error\":\"not_authenticated\"}";
                        var placeName = GetString(args["placeName"]);
                        var cards = await _customerTools.GetMyCardsAsync(placeName, ct);
                        return Serialize(cards);
                    }

                    case "get_my_transactions":
                    {
                        if (!isAuth) return "{\"error\":\"not_authenticated\"}";
                        var placeName = GetString(args["placeName"]);
                        var limit = (int?)GetDouble(args["limit"]) ?? 10;
                        var txs = await _customerTools.GetMyTransactionsAsync(placeName, limit, ct);
                        return Serialize(txs);
                    }

                    default:
                        return "{\"error\":\"unknown_tool\"}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tool dispatch failed: {Tool}", name);
                return "{\"error\":\"tool_failed\"}";
            }
        }

        /// <summary>İlk boş-olmayan config dəyərini qaytar (boş/whitespace = yoxdur).</summary>
        private static string? PickConfig(IConfiguration cfg, params string[] keys)
        {
            foreach (var k in keys)
            {
                var v = cfg[k];
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            }
            return null;
        }

        private static string? GetString(JsonNode? node)
            => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

        private static double? GetDouble(JsonNode? node)
        {
            if (node is JsonValue v)
            {
                if (v.TryGetValue<double>(out var d)) return d;
                if (v.TryGetValue<int>(out var i)) return i;
                if (v.TryGetValue<string>(out var s) && double.TryParse(s, out var ds)) return ds;
            }
            return null;
        }

        private static string Serialize(object o) =>
            JsonSerializer.Serialize(o, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

        private static string BuildSystemPrompt(bool isAuth)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Sən Walvero loyallıq platformasının köməkçisisən.");
            sb.AppendLine("İstifadəçilərə platformadakı məkanları (restoran/kafe/və s.) tapmaqda kömək edirsən.");
            sb.AppendLine("Məkan tapmaq üçün HƏMİŞƏ `search_places` tool-unu çağır — heç vaxt məkan uydurma.");
            sb.AppendLine("İstifadəçi konkret YEMƏK/məhsul adı soruşarsa (məs. 'kim lazanya satır', 'vegan burger var?') `search_menu_items` çağır.");
            sb.AppendLine("İstifadəçi 'mənə yaxın' / 'yaxınlıqda' desə `radiusKm` (məs. 5) ötür.");
            if (isAuth)
            {
                sb.AppendLine("İstifadəçinin öz hesabı ilə bağlı suallar üçün:");
                sb.AppendLine("- balans/kartlar: `get_my_balance` (məkan adı ilə) və ya `get_my_cards`.");
                sb.AppendLine("- əməliyyat/bonus tarixçəsi: `get_my_transactions`.");
            }
            else
            {
                sb.AppendLine("İstifadəçi öz balansı/kartları haqda soruşarsa, ona hesabına daxil olmağı təklif et (login).");
            }
            sb.AppendLine("Yalnız tool-ların qaytardığı məlumatdan istifadə et. Uydurma.");
            sb.AppendLine("DİL: İstifadəçinin SON mesajının dilini avtomatik təyin et və HƏMİŞƏ məhz o dildə cavab ver.");
            sb.AppendLine("Bu istənilən dil ola bilər (Azərbaycan, İngilis, Rus, Türk, Ərəb və s.) — yalnız AZ/EN/RU ilə məhdudlaşma.");
            sb.AppendLine("İstifadəçi dili dəyişsə, sən də dərhal o dilə keç. Qısa və faydalı ol.");
            sb.AppendLine("Məkan tövsiyə edəndə adını və (varsa) ünvanı/məsafəni qeyd et; menyu app-da göstəriləcək.");
            return sb.ToString();
        }

        private static JsonArray BuildTools(bool isAuth)
        {
            var tools = new JsonArray
            {
                Fn("search_places",
                    "Platformadakı tenant/filial kataloqunda məkan axtar (cuisine, xüsusiyyət, ərazi, yaxınlıq).",
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["keywords"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject { ["type"] = "string" },
                                ["description"] = "Açar sözlər: cuisine (burger), xüsusiyyət (qəlyan, wifi), ərazi adı (Nizami)."
                            },
                            ["categoryName"] = new JsonObject { ["type"] = "string", ["description"] = "Biznes kateqoriyası, məs. Restoran, Kafe." },
                            ["city"] = new JsonObject { ["type"] = "string" },
                            ["radiusKm"] = new JsonObject { ["type"] = "number", ["description"] = "Yalnız yaxınlıq istənəndə (məs. 'mənə yaxın'). Kilometrlə." }
                        },
                        ["required"] = new JsonArray { "keywords" }
                    }),
                Fn("search_menu_items",
                    "Konkret YEMƏK/məhsul adına görə onu menyusunda satan məkanları tap (məs. 'lazanya', 'vegan burger', 'sushi'). İstifadəçi məkanı deyil, yeməyi soruşanda bunu işlət.",
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["keywords"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject { ["type"] = "string" },
                                ["description"] = "Yemək/məhsul adı sözləri, məs. ['lazanya'] və ya ['vegan','burger']."
                            },
                            ["radiusKm"] = new JsonObject { ["type"] = "number", ["description"] = "Yalnız yaxınlıq istənəndə. Kilometrlə." }
                        },
                        ["required"] = new JsonArray { "keywords" }
                    })
            };

            if (isAuth)
            {
                tools.Add(Fn("get_my_cards",
                    "İstifadəçinin loyallıq kartları və xal balansları (opsional məkan adı ilə süz).",
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["placeName"] = new JsonObject { ["type"] = "string", ["description"] = "Məkan/brend adı (opsional)." }
                        }
                    }));

                tools.Add(Fn("get_my_balance",
                    "İstifadəçinin konkret məkandakı xal balansı.",
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["placeName"] = new JsonObject { ["type"] = "string", ["description"] = "Məkan/brend adı." }
                        },
                        ["required"] = new JsonArray { "placeName" }
                    }));

                tools.Add(Fn("get_my_transactions",
                    "İstifadəçinin əməliyyat/bonus tarixçəsi (opsional məkan + limit).",
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["placeName"] = new JsonObject { ["type"] = "string" },
                            ["limit"] = new JsonObject { ["type"] = "integer", ["description"] = "Neçə əməliyyat (default 10)." }
                        }
                    }));
            }

            return tools;
        }

        private static JsonObject Fn(string name, string description, JsonObject parameters) => new()
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["parameters"] = parameters
            }
        };
    }
}
