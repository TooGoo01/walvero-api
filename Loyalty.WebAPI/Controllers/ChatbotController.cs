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
        private readonly ITenantProvider _tenantProvider;
        private readonly IMemoryCache _cache;

        // Yüngül rate-limit: eyni istifadəçi/IP-dən dəqiqədə N sorğu (spam + LLM xərci qoruması).
        private const int MaxPerMinute = 20;

        public ChatbotController(
            IPlaceAssistantService assistant,
            IPlaceSearchService placeSearch,
            ITenantProvider tenantProvider,
            IMemoryCache cache)
        {
            _assistant = assistant;
            _placeSearch = placeSearch;
            _tenantProvider = tenantProvider;
            _cache = cache;
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
