using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Dtos.Post.Main;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class ApplePassBuilder : IApplePassBuilder
    {
        private readonly IAppEnvironment _env;
        private readonly FileDownloader _file;
        private readonly ILogger<ApplePassBuilder> _log;

        public ApplePassBuilder(IAppEnvironment env, FileDownloader file, ILogger<ApplePassBuilder> log)
        {
            _env = env;
            _file = file;
            _log = log;
        }

        public async Task BuildAsync(BuildPassRequest req, string workingDir, CancellationToken ct = default)
        {
            Directory.CreateDirectory(workingDir);

            // --- Şəkillər (PNG, Apple spec ölçülərinə salınmış) ---
            // icon.png Apple üçün məcburidir — logo endirilə bilməsə fallback yaradılır ki,
            // pass generasiyası şəkil probleminə görə heç vaxt uğursuz olmasın.
            // Ölçüyə salma vacibdir: orijinal böyük şəkillər (foto ölçüsündə strip/logo)
            // zəif iPhone modellərində pass-ın quraşdırılmasını sındırır və pkpass-ı şişirdir.
            using (var logoImg = await TryLoadImageAsync(req.LogoUrl, ct)
                                 ?? CreateFallbackLogo(req))
            {
                // logo: yuxarı sol künc, @1x maks 160x50pt
                await SaveResizedAsync(logoImg, Path.Combine(workingDir, "logo.png"), 160, 50, ResizeMode.Max, ct);
                await SaveResizedAsync(logoImg, Path.Combine(workingDir, "logo@2x.png"), 320, 100, ResizeMode.Max, ct);

                // icon: kvadrat, @1x 29x29 (Pad — nisbət pozulmasın)
                await SaveResizedAsync(logoImg, Path.Combine(workingDir, "icon.png"), 29, 29, ResizeMode.Pad, ct);
                await SaveResizedAsync(logoImg, Path.Combine(workingDir, "icon@2x.png"), 58, 58, ResizeMode.Pad, ct);
                await SaveResizedAsync(logoImg, Path.Combine(workingDir, "icon@3x.png"), 87, 87, ResizeMode.Pad, ct);
            }

            if (!string.IsNullOrWhiteSpace(req.StripImageUrl))
            {
                using var stripImg = await TryLoadImageAsync(req.StripImageUrl, ct);
                if (stripImg is not null)
                {
                    // storeCard strip: @1x 375x123 — Crop (cover) ki, foto düzgün doldursun
                    await SaveResizedAsync(stripImg, Path.Combine(workingDir, "strip.png"), 375, 123, ResizeMode.Crop, ct);
                    await SaveResizedAsync(stripImg, Path.Combine(workingDir, "strip@2x.png"), 750, 246, ResizeMode.Crop, ct);
                }
                else
                {
                    _log.LogWarning(
                        "Pass strip image unavailable (url={StripUrl}, serial={Serial}) — building pass without strip",
                        req.StripImageUrl, req.SerialNumber);
                }
            }

            // --- pass.json ---
            var pass = BuildPassJson(req);
            var json = JsonSerializer.Serialize(pass, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            });
            await File.WriteAllTextAsync(Path.Combine(workingDir, "pass.json"), json, ct);
        }
        /// <summary>
        /// URL-dən şəkli endirib istənilən formatdan (JPG/WebP/PNG) yükləyir.
        /// Uğursuzluqda null qaytarır — pass generasiyası davam edir.
        /// </summary>
        private async Task<Image?> TryLoadImageAsync(string? url, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            var tmp = Path.Combine(Path.GetTempPath(), $"pass-img-{Guid.NewGuid():N}.download");
            try
            {
                await _file.DownloadToAsync(url, tmp, ct);
                return await Image.LoadAsync(tmp, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Pass image download/load failed: {Url}", url);
                return null;
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* ignore */ }
            }
        }

        /// <summary>
        /// Pass-ın fon rəngində boş kvadrat loqo — fonla qaynayıb görünməz olur,
        /// amma məcburi icon.png tələbini ödəyir.
        /// </summary>
        private Image CreateFallbackLogo(BuildPassRequest req)
        {
            _log.LogWarning(
                "Pass logo unavailable (url={LogoUrl}, serial={Serial}) — using generated fallback logo",
                req.LogoUrl, req.SerialNumber);
            var color = ParseColorOrDefault(req.BackgroundColor, new Rgba32(45, 45, 45));
            return new Image<Rgba32>(300, 300, color);
        }

        private static async Task SaveResizedAsync(Image source, string path, int width, int height, ResizeMode mode, CancellationToken ct)
        {
            using var clone = source.CloneAs<Rgba32>();
            clone.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = mode
            }));
            await clone.SaveAsPngAsync(path, ct);
        }

        private static Rgba32 ParseColorOrDefault(string? value, Rgba32 fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            var v = value.Trim();
            try
            {
                if (v.StartsWith("#"))
                    return Color.ParseHex(v).ToPixel<Rgba32>();

                if (v.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = v[(v.IndexOf('(') + 1)..v.IndexOf(')')].Split(',');
                    if (parts.Length >= 3 &&
                        byte.TryParse(parts[0].Trim(), out var r) &&
                        byte.TryParse(parts[1].Trim(), out var g) &&
                        byte.TryParse(parts[2].Trim(), out var b))
                        return new Rgba32(r, g, b);
                }
            }
            catch { /* fallback aşağıda */ }
            return fallback;
        }
        private static object BuildPassJson(BuildPassRequest req)
        {
            var storeCard = new Dictionary<string, object?>();


            static object SerializeField(PassField f)
            {
                var dict = new Dictionary<string, object>
                {
                    ["key"] = f.Key,
                    ["value"] = f.Value ?? string.Empty
                };
                if (!string.IsNullOrWhiteSpace(f.Label))
                    dict["label"] = f.Label!;
                if (!string.IsNullOrWhiteSpace(f.ChangeMessage))
                    dict["changeMessage"] = f.ChangeMessage!;
                return dict;
            }

            if (req.PrimaryFields?.Any() == true)
                storeCard["primaryFields"] = req.PrimaryFields.Select(SerializeField).ToArray();

            if (req.SecondaryFields?.Any() == true)
                storeCard["secondaryFields"] = req.SecondaryFields.Select(SerializeField).ToArray();
            if (req.HeaderFields?.Any() == true)
                storeCard["headerFields"] = req.HeaderFields.Select(SerializeField).ToArray();
            if (req.BackFields?.Any() == true)
                storeCard["backFields"] = req.BackFields.Select(SerializeField).ToArray();
            // >>> ADI QR-KODUN ÜSTÜNDƏ GÖSTƏRMƏK
            var aux = new List<object>();
            if (req.AuxiliaryFields?.Any() == true)
                aux.AddRange(req.AuxiliaryFields.Select(SerializeField));

          
            if (aux.Count > 0)
                storeCard["auxiliaryFields"] = aux.ToArray();

            // >>> QR-KOD ALT MƏTNİ (altText)
            var barcodeObj = new
            {
                message = req.BarcodeMessage,              // skan olunacaq dəyər
                format = req.BarcodeFormat,                // "PKBarcodeFormatQR", "PKBarcodeFormatPDF417", "PKBarcodeFormatCode128"...
                messageEncoding = "iso-8859-1",
                altText = string.IsNullOrWhiteSpace(req.Barcode)
                            ? req.BarcodeMessage           // altına göstəriləcək yazı
                            : req.Barcode
            };

            // (istəsən uyğunluq üçün "barcodes" massivini də əlavə edə bilərsən)
            var root = new Dictionary<string, object?>
            {
                ["formatVersion"] = 1,
                ["passTypeIdentifier"] = req.PassTypeIdentifier,
                ["serialNumber"] = req.SerialNumber,
                ["teamIdentifier"] = req.TeamIdentifier,
                ["organizationName"] = req.OrganizationName,
                ["description"] = req.Description,
                ["logoText"] = req.LogoText,
                ["backgroundColor"] = NormalizeColor(req.BackgroundColor),
                ["foregroundColor"] = NormalizeColor(req.ForegroundColor ?? "#000000"),
                ["labelColor"] = NormalizeColor(req.LabelColor ?? "#444444"),
                ["barcode"] = barcodeObj,                  // köhnə sahə, hələ də dəstəklənir
                ["barcodes"] = new[] { barcodeObj },       // yeni sxemə uyğun
                ["storeCard"] = storeCard,
                ["groupingIdentifier"] = req.SerialNumber,
                ["lastUpdated" ] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            if (!string.IsNullOrWhiteSpace(req.WebServiceURL))
                root["webServiceURL"] = req.WebServiceURL;
            if (!string.IsNullOrWhiteSpace(req.AuthenticationToken))
                root["authenticationToken"] = req.AuthenticationToken;

            // Location-based triggers (branch locations)
            if (req.Locations?.Count > 0)
            {
                root["locations"] = req.Locations
                    .Select(loc => new Dictionary<string, object>
                    {
                        ["latitude"] = loc.Latitude,
                        ["longitude"] = loc.Longitude,
                        ["relevantText"] = loc.RelevantText ?? ""
                    })
                    .ToArray();
            }

            return root;
        }


        // HEX → rgb() converter (Apple Wallet pass.json requires rgb(r,g,b) format)
        private static string NormalizeColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return "rgb(0, 0, 0)";

            color = color.Trim();

            // Əgər artıq rgb() formatındadırsa — olduğu kimi qaytar
            if (color.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
                return color;

            // HEX → rgb() çevir
            if (color.StartsWith("#") && (color.Length == 7 || color.Length == 4))
            {
                string hex = color.Length == 4
                    ? $"#{color[1]}{color[1]}{color[2]}{color[2]}{color[3]}{color[3]}"
                    : color;

                if (int.TryParse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var r) &&
                    int.TryParse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
                    int.TryParse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
                {
                    return $"rgb({r}, {g}, {b})";
                }
            }

            return color;
        }

        // RGB → HEX converter
       
    }
}
