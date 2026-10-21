using System.Text.RegularExpressions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Loyalty.WebAPI.Extensions
{
    /// <summary>
    /// r_keeper FarCards URL-də wallet token var (api/rkeeper/{token}/...) — App Insights request
    /// telemetriyasına (Url, Name) düşməsin deyə "***" ilə əvəz olunur.
    /// </summary>
    public sealed class RkeeperTokenTelemetryInitializer : ITelemetryInitializer
    {
        private static readonly Regex TokenSegment = new(
            @"(/api/rkeeper/)[^/?#]+", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is not RequestTelemetry request)
                return;

            if (request.Url != null && request.Url.AbsolutePath.Contains("/api/rkeeper/", StringComparison.OrdinalIgnoreCase))
                request.Url = new Uri(Mask(request.Url.ToString()));

            if (!string.IsNullOrEmpty(request.Name) && request.Name.Contains("/api/rkeeper/", StringComparison.OrdinalIgnoreCase))
                request.Name = Mask(request.Name);
        }

        public static string Mask(string value) => TokenSegment.Replace(value, "$1***");
    }
}
