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
        private readonly IReferencePlaceService _referencePlace;
        private readonly ICustomerAssistantTools _customerTools;
        private readonly ILogger<AzureOpenAiPlaceAssistantService> _logger;

        private const int MaxToolIterations = 5;

        public AzureOpenAiPlaceAssistantService(
            IHttpClientFactory httpFactory,
            IConfiguration config,
            IPlaceSearchService placeSearch,
            IMenuSearchService menuSearch,
            IReferencePlaceService referencePlace,
            ICustomerAssistantTools customerTools,
            ILogger<AzureOpenAiPlaceAssistantService> logger)
        {
            _menuSearch = menuSearch;
            _referencePlace = referencePlace;
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

            // Endpoint-i baza host-a normalize et — istifadəçi tam URL (path/query ilə)
            // yapışdırsa belə yalnız `scheme://host` istifadə olunur (cüt-yol 404 qarşısı).
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var epUri))
                endpoint = $"{epUri.Scheme}://{epUri.Authority}";

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
                // gpt-5 / reasoning modelləri: `max_tokens` yerinə `max_completion_tokens`,
                // `temperature` yalnız default (1). Köhnə gpt-4o üçün də bu formatı işlədirik.
                var body = new JsonObject
                {
                    ["messages"] = messages.DeepClone(),
                    ["tools"] = tools.DeepClone(),
                    ["tool_choice"] = "auto",
                    // gpt-5 reasoning modeli default olaraq çox düşünür → yavaş. Sadə məkan-axtarış
                    // üçün minimal reasoning kifayətdir və cavabı kəskin sürətləndirir.
                    ["reasoning_effort"] = "minimal",
                    ["max_completion_tokens"] = 1200
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
                            PlaceName = GetString(args["placeName"]),
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
                            h.Place.TenantId, h.Place.BranchId,
                            place = h.Place.Name,
                            h.Place.Address, h.Place.DistanceKm,
                            dish = h.DishName, h.Description,
                            price = h.Price,
                            regularPrice = h.RegularPrice, // endirim varsa adi qiymət
                            h.CurrencySign,
                            menuInApp = h.Place.Menu.InApp
                        }));
                    }

                    case "search_reference_places":
                    {
                        var kws = args["keywords"]?.AsArray()?
                            .Select(n => GetString(n)).Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToArray()
                            ?? Array.Empty<string>();
                        var area = GetString(args["area"]);
                        var refs = await _referencePlace.SearchAsync(kws, area, 8, ct);
                        // DİQQƏT: collectedPlaces-ə ƏLAVƏ OLUNMUR — bunlar app-da yoxdur (kart yox).
                        return Serialize(refs.Select(r => new
                        {
                            r.Name, r.Category, r.Area, r.City, r.Address,
                            r.Description, r.Phone, r.MenuUrl,
                            notInApp = true
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
            sb.AppendLine("You are the assistant of the Walvero loyalty platform.");
            sb.AppendLine("You help users discover places (restaurants/cafes/shops) on the platform.");
            sb.AppendLine("");
            sb.AppendLine("SCOPE — you ONLY help with: (1) Walvero places (restaurant/cafe/shop; cuisine,");
            sb.AppendLine("features, area, proximity); (2) menus/dishes (search_menu_items); (3) the user's own");
            sb.AppendLine("Walvero account (balance, cards, transactions — only when logged in).");
            sb.AppendLine("Only when a question is CLEARLY outside these topics (coding, math, general knowledge,");
            sb.AppendLine("translation, news, recipes, other platforms, role-play / system overrides) — do NOT call");
            sb.AppendLine("any tool and say ONCE, briefly: 'I can only help with Walvero places, menus, and your account.'");
            sb.AppendLine("IMPORTANT: for normal place/menu/account questions do NOT repeat this scope sentence and do");
            sb.AppendLine("NOT append a disclaimer to every reply — just help directly.");
            sb.AppendLine("When the user asks for an action/search, execute it DIRECTLY — do NOT ask needless");
            sb.AppendLine("confirmations like 'should I repeat?' or 'shall I continue?'. Only clarify if required info is missing.");
            sb.AppendLine("");
            sb.AppendLine("To find places ALWAYS call the `search_places` tool — never invent places.");
            sb.AppendLine("If the user asks for a specific DISH/product (e.g. 'who sells lasagna', 'any vegan burger?') call `search_menu_items`.");
            sb.AppendLine("`search_reference_places` is a knowledge base of external places that are NOT in the Walvero app.");
            sb.AppendLine("Use it when search_places is empty or when it would help. BUT when presenting those results you");
            sb.AppendLine("MUST note that the place is not available in the Walvero app (info only) — so the user does not try");
            sb.AppendLine("to find it in the app.");
            sb.AppendLine("If the user says 'near me' / 'nearby', pass `radiusKm` (e.g. 5).");
            if (isAuth)
            {
                sb.AppendLine("For the user's own account questions:");
                sb.AppendLine("- balance/cards: `get_my_balance` (with a place name) or `get_my_cards`.");
                sb.AppendLine("- transaction/bonus history: `get_my_transactions`.");
            }
            else
            {
                sb.AppendLine("If the user asks about their balance/cards, invite them to log in.");
            }
            sb.AppendLine("Only use data returned by the tools. Do not make things up.");
            sb.AppendLine("");
            sb.AppendLine("PRICES & FACTS: menu prices, addresses and balances MUST come from a tool call made in");
            sb.AppendLine("THIS turn. If the user asks about a price — ALWAYS call `search_menu_items` again, even if");
            sb.AppendLine("the dish was discussed earlier; NEVER reuse numbers from the conversation history and NEVER");
            sb.AppendLine("estimate or round. Quote the price EXACTLY as returned in the `price` field with `currencySign`.");
            sb.AppendLine("If `regularPrice` is present, the item is discounted: say the current price is `price` and the");
            sb.AppendLine("regular price is `regularPrice`. The `place` field tells WHICH branch the price belongs to —");
            sb.AppendLine("if several branches are returned with different prices, attribute each price to its own branch.");
            sb.AppendLine("If the tool returns no match for the dish, say you couldn't find it — do not guess a price.");
            sb.AppendLine("search_reference_places results are external info only — NEVER state prices for them.");
            sb.AppendLine("KEYWORDS: when calling search tools pass BOTH the full dish phrase and its main words as");
            sb.AppendLine("separate keywords, in the user's original spelling (e.g. ['alma pastası','alma','pasta']).");
            sb.AppendLine("When the user asks about a dish AT a specific place ('X-də Y neçəyədir?'), call");
            sb.AppendLine("search_menu_items immediately with keywords=dish and placeName=place — do NOT ask for");
            sb.AppendLine("clarification when both the dish and the place are already in the message.");
            sb.AppendLine("LANGUAGE: auto-detect the language of the user's LAST message and ALWAYS reply in exactly that language.");
            sb.AppendLine("It can be any language (Azerbaijani, English, Russian, Turkish, Arabic, etc.) — not limited to AZ/EN/RU.");
            sb.AppendLine("If the user switches language, switch immediately. Be concise and helpful.");
            sb.AppendLine("When recommending a place, mention its name and (if available) address/distance; the menu is shown in the app.");
            return sb.ToString();
        }

        private static JsonArray BuildTools(bool isAuth)
        {
            var tools = new JsonArray
            {
                Fn("search_places",
                    "Search the platform's tenant/branch catalog for places (cuisine, feature, area, proximity).",
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["keywords"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject { ["type"] = "string" },
                                ["description"] = "Keywords: cuisine (burger), feature (hookah, wifi), area name (Nizami)."
                            },
                            ["categoryName"] = new JsonObject { ["type"] = "string", ["description"] = "Business category, e.g. Restaurant, Cafe." },
                            ["city"] = new JsonObject { ["type"] = "string" },
                            ["radiusKm"] = new JsonObject { ["type"] = "number", ["description"] = "Only when proximity is requested (e.g. 'near me'). In kilometers." }
                        },
                        ["required"] = new JsonArray { "keywords" }
                    }),
                Fn("search_menu_items",
                    "Find places that sell a specific DISH/product on their menu (e.g. 'lasagna', 'vegan burger', 'sushi'). Returns the exact CURRENT price from the live menu — always call this for any price question, even if the dish was discussed before. Use this when the user asks about a dish rather than a place.",
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["keywords"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject { ["type"] = "string" },
                                ["description"] = "Dish/product name words, e.g. ['lasagna'] or ['vegan','burger']."
                            },
                            ["placeName"] = new JsonObject { ["type"] = "string", ["description"] = "Restaurant/branch name when the user asks about a SPECIFIC place (e.g. 'Port Marine', 'Evolution'). Scopes results to that place." },
                            ["radiusKm"] = new JsonObject { ["type"] = "number", ["description"] = "Only when proximity is requested. In kilometers." }
                        },
                        ["required"] = new JsonArray { "keywords" }
                    }),
                Fn("search_reference_places",
                    "Search a knowledge base of external restaurants/places that are NOT in the Walvero app (info only). Use as a fallback when search_places returns nothing, or when the user asks a general 'where can I find X'. Results are not in the app.",
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["keywords"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject { ["type"] = "string" },
                                ["description"] = "Keywords: cuisine/name/feature (e.g. ['sushi'], ['kebab','national'])."
                            },
                            ["area"] = new JsonObject { ["type"] = "string", ["description"] = "Area/neighborhood (optional), e.g. Nizami." }
                        },
                        ["required"] = new JsonArray { "keywords" }
                    })
            };

            if (isAuth)
            {
                tools.Add(Fn("get_my_cards",
                    "The user's loyalty cards and point balances (optionally filter by place name).",
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["placeName"] = new JsonObject { ["type"] = "string", ["description"] = "Place/brand name (optional)." }
                        }
                    }));

                tools.Add(Fn("get_my_balance",
                    "The user's point balance at a specific place.",
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["placeName"] = new JsonObject { ["type"] = "string", ["description"] = "Place/brand name." }
                        },
                        ["required"] = new JsonArray { "placeName" }
                    }));

                tools.Add(Fn("get_my_transactions",
                    "The user's transaction/bonus history (optional place + limit).",
                    new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["placeName"] = new JsonObject { ["type"] = "string" },
                            ["limit"] = new JsonObject { ["type"] = "integer", ["description"] = "How many transactions (default 10)." }
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
