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
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                // Yalnız icazəli rejimlər — default driving
                var travelMode = mode?.ToLowerInvariant() == "walking" ? "walking" : "driving";
                var url = "https://maps.googleapis.com/maps/api/directions/json" +
                          $"?origin={fromLat.ToString(ci)},{fromLng.ToString(ci)}" +
                          $"&destination={toLat.ToString(ci)},{toLng.ToString(ci)}" +
                          $"&mode={travelMode}&language=az&key={apiKey}";

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                using var resp = await client.GetAsync(url, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var status = root.GetProperty("status").GetString();
                if (status != "OK")
                {
                    _logger.LogWarning("Directions API status={Status}", status);
                    return Ok(new { success = false, message = status });
                }

                var route = root.GetProperty("routes")[0];
                var leg = route.GetProperty("legs")[0];
                return Ok(new
                {
                    success = true,
                    points = route.GetProperty("overview_polyline").GetProperty("points").GetString(),
                    distanceText = leg.GetProperty("distance").GetProperty("text").GetString(),
                    distanceMeters = leg.GetProperty("distance").GetProperty("value").GetInt32(),
                    durationText = leg.GetProperty("duration").GetProperty("text").GetString(),
                    durationSeconds = leg.GetProperty("duration").GetProperty("value").GetInt32()
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
