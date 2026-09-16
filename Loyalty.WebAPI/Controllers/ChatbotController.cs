using Loyalty.Business.Dtos.Chatbot;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ChatbotController : ControllerBase
    {
        private readonly IPlaceAssistantService _assistant;
        private readonly IPlaceSearchService _placeSearch;
        private readonly IKeywordGenerationService _keywordGen;
        private readonly ITenantProvider _tenantProvider;
        private readonly IMemoryCache _cache;

        // Yüngül rate-limit: eyni istifadəçi/IP-dən dəqiqədə N sorğu (spam + LLM xərci qoruması).
        private const int MaxPerMinute = 20;

        public ChatbotController(
            IPlaceAssistantService assistant,
            IPlaceSearchService placeSearch,
            IKeywordGenerationService keywordGen,
            ITenantProvider tenantProvider,
            IMemoryCache cache)
        {
            _assistant = assistant;
            _placeSearch = placeSearch;
            _keywordGen = keywordGen;
            _tenantProvider = tenantProvider;
            _cache = cache;
        }

        /// <summary>Super-admin: tenant üçün AI ilə açar sözləri generasiya et (redaktəyə təklif).</summary>
        [HttpPost]
        [Authorize(
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
            Policy = "SuperAdminOnly")]
        public async Task<IActionResult> GenerateKeywords([FromQuery] int tenantId, CancellationToken ct)
        {
            var result = await _keywordGen.GenerateForTenantAsync(tenantId, ct);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        /// <summary>
        /// Super-admin AI səhifəsi — keyword-lərin necə işlədiyini test et (LLM-siz, birbaşa
        /// PlaceSearchService). Azure OpenAI qoşulmasa belə işləyir.
        /// </summary>
        [HttpGet]
        [Authorize(
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
            Policy = "SuperAdminOnly")]
        public async Task<IActionResult> TestSearch(
            [FromQuery] string q,
            [FromQuery] double? lat,
            [FromQuery] double? lng,
            [FromQuery] double? radiusKm,
            CancellationToken ct)
        {
            var keywords = (q ?? string.Empty)
                .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            var query = new Loyalty.Business.Dtos.Chatbot.PlaceSearchQuery
            {
                Keywords = keywords,
                Lat = lat,
                Lng = lng,
                RadiusKm = radiusKm,
                Take = 12
            };

            var places = await _placeSearch.SearchAsync(query, ct);
            return Ok(new ServiceResult(true, places));
        }

        // MÜVƏQQƏTİ DIAQNOSTİKA (anonim — chatbot /Ask onsuz da eyni datanı anonim verir).
        // Tool-lar prod-da "tool_failed" qaytarır; exception mətnini görmək üçün.
        // Fix tapılan kimi SİLİNƏCƏK.
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> DiagSearch(
            [FromQuery] string q,
            [FromQuery] string? place,
            [FromServices] IMenuSearchService menuSearch,
            CancellationToken ct)
        {
            var report = new System.Collections.Generic.Dictionary<string, object?>();
            try
            {
                var places = await _placeSearch.SearchAsync(new PlaceSearchQuery
                {
                    Keywords = (q ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                }, ct);
                report["places"] = places.Count;
            }
            catch (Exception ex)
            {
                report["placesError"] = $"{ex.GetType().Name}: {ex.Message} | inner: {ex.InnerException?.Message}";
            }
            try
            {
                var hits = await menuSearch.SearchAsync(new MenuItemSearchQuery
                {
                    Keywords = (q ?? "").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries),
                    PlaceName = place
                }, ct);
                report["menuHits"] = hits.Count;
            }
            catch (Exception ex)
            {
                report["menuError"] = $"{ex.GetType().Name}: {ex.Message} | inner: {ex.InnerException?.Message}";
            }
            return Ok(report);
        }

        /// <summary>
        /// Chatbot sorğusu. Anonim (məkan-kəşf) və ya JWT-li (şəxsi hesab) işləyir.
        /// JWT gələrsə şəxsi tool-lar avtomatik aktivləşir.
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Ask([FromBody] ChatbotAskRequest request, CancellationToken ct)
        {
            if (request?.Messages is null || request.Messages.Count == 0)
                return Ok(ServiceResult<AssistantReply>.Fail("Mesaj boşdur."));

            // Son user mesajının uzunluq limiti (abuse qoruması).
            var lastUser = request.Messages.LastOrDefault(m =>
                string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (lastUser is not null && (lastUser.Content?.Length ?? 0) > 1000)
                return Ok(ServiceResult<AssistantReply>.Fail("Sual çox uzundur."));

            if (!AllowRequest())
                return Ok(ServiceResult<AssistantReply>.Fail("Çox sayda sorğu göndərildi, bir azdan yenidən cəhd edin."));

            var reply = await _assistant.AskAsync(request, ct);
            return Ok(ServiceResult<AssistantReply>.Ok(reply));
        }

        private bool AllowRequest()
        {
            var identity = _tenantProvider.CurrentUserId
                ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anon";
            var key = $"chatbot:rl:{identity}";
            var count = _cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return 0;
            });
            if (count >= MaxPerMinute) return false;
            _cache.Set(key, count + 1, TimeSpan.FromMinutes(1));
            return true;
        }
    }
}
