using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// Mobil app-ın xəritə funksiyaları üçün proxy. Google Directions API server açarı ilə
    /// buradan çağırılır — Android-restricted key HTTP sorğularda işləmədiyi üçün.
    /// Açar yoxdursa/sorğu uğursuzdursa success=false qayıdır → app düz xətt fallback çəkir.
    /// </summary>
    [Route("api/public/map")]
    [ApiController]
    [AllowAnonymous]
    public class MapController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MapController> _logger;

        public MapController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<MapController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("directions")]
        [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "fromLat", "fromLng", "toLat", "toLng", "mode" })]
        public async Task<IActionResult> Directions(
            [FromQuery] double fromLat,
            [FromQuery] double fromLng,
            [FromQuery] double toLat,
            [FromQuery] double toLng,
            [FromQuery] string? mode,
            CancellationToken ct)
        {
            var apiKey = _configuration["GoogleMaps:ServerApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return Ok(new { success = false, message = "Directions key konfiqurasiya olunmayıb" });

            try
            {
                // Routes API (computeRoutes) — legacy Directions API yeni açarlarla
                // OVER_QUERY_LIMIT qaytarır; Google yeni inteqrasiyaları buna yönləndirir.
                var travelMode = mode?.ToLowerInvariant() == "walking" ? "WALK" : "DRIVE";
                var requestBody = new
                {
                    origin = new { location = new { latLng = new { latitude = fromLat, longitude = fromLng } } },
                    destination = new { location = new { latLng = new { latitude = toLat, longitude = toLng } } },
                    travelMode,
                    languageCode = "az"
                };

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                using var req = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://routes.googleapis.com/directions/v2:computeRoutes");
                req.Headers.Add("X-Goog-Api-Key", apiKey);
                req.Headers.Add("X-Goog-FieldMask",
                    "routes.polyline.encodedPolyline,routes.distanceMeters,routes.duration");
                req.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json");

                using var resp = await client.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Routes API HTTP {Status}: {Body}", (int)resp.StatusCode,
                        body.Length > 400 ? body[..400] : body);
                    return Ok(new { success = false, message = $"Routes API xətası ({(int)resp.StatusCode})" });
                }

                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("routes", out var routes) ||
                    routes.GetArrayLength() == 0)
                {
                    return Ok(new { success = false, message = "Marşrut tapılmadı" });
                }

                var route = routes[0];
                var points = route.GetProperty("polyline").GetProperty("encodedPolyline").GetString();
                var distanceMeters = route.TryGetProperty("distanceMeters", out var dm) ? dm.GetInt32() : 0;
                // duration "123s" formatındadır
                var durationStr = route.TryGetProperty("duration", out var du) ? du.GetString() ?? "0s" : "0s";
                var durationSeconds = int.TryParse(durationStr.TrimEnd('s'), out var ds) ? ds : 0;

                var distanceText = distanceMeters >= 1000
                    ? $"{distanceMeters / 1000.0:0.#} km"
                    : $"{distanceMeters} m";
                var durationText = durationSeconds >= 3600
                    ? $"{durationSeconds / 3600} saat {(durationSeconds % 3600) / 60} dəq"
                    : $"{Math.Max(1, durationSeconds / 60)} dəq";

                return Ok(new
                {
                    success = true,
                    points,
                    distanceText,
                    distanceMeters,
                    durationText,
                    durationSeconds
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Directions proxy failed");
                return Ok(new { success = false, message = "Directions alınmadı" });
            }
        }
    }
}
