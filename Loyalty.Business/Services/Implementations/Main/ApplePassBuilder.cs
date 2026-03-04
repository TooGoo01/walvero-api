using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Dtos.Post.Main;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class ApplePassBuilder : IApplePassBuilder
    {
        private readonly IAppEnvironment _env;
        private readonly FileDownloader _file;

        public ApplePassBuilder(IAppEnvironment env, FileDownloader file)
        {
            _env = env;
            _file = file;
        }

        public async Task BuildAsync(BuildPassRequest req, string workingDir, CancellationToken ct = default)
        {
            Directory.CreateDirectory(workingDir);

            // --- Şəkillər (PNG) ---
            if (!string.IsNullOrWhiteSpace(req.LogoUrl))
            {
                await _file.DownloadToAsync(req.LogoUrl!, Path.Combine(workingDir, "logo.png"), ct);
                await _file.DownloadToAsync(req.LogoUrl!, Path.Combine(workingDir, "icon.png"), ct);
                await _file.DownloadToAsync(req.LogoUrl!, Path.Combine(workingDir, "icon@2x.png"), ct);
                await EnsurePng(Path.Combine(workingDir, "icon.png"));
                await EnsurePng(Path.Combine(workingDir, "logo.png"));

                //await EnsurePng(Path.Combine(workingDir, "icon@2x.png"));
            }
           
            if (!string.IsNullOrWhiteSpace(req.StripImageUrl))
            {
                await _file.DownloadToAsync(req.StripImageUrl!, Path.Combine(workingDir, "strip.png"), ct);
                await EnsurePng(Path.Combine(workingDir, "strip.png"));
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
        async Task EnsurePng(string path)
        {
            if (!File.Exists(path)) throw new Exception($"{Path.GetFileName(path)} tapılmadı");
            var b = await File.ReadAllBytesAsync(path);
            if (b.Length < 100) throw new Exception($"{Path.GetFileName(path)} çox kiçik ({b.Length} B)");
            var sig = b.Take(8).ToArray();
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            if (!sig.SequenceEqual(png)) throw new Exception($"{Path.GetFileName(path)} PNG deyil");
        }
        private static object BuildPassJson(BuildPassRequest req)
        {
            var storeCard = new Dictionary<string, object?>();


            if (req.PrimaryFields?.Any() == true)
                storeCard["primaryFields"] = req.PrimaryFields
                    .Select(f => new { key = f.Key, label = f.Label, value = f.Value }).ToArray();

            if (req.SecondaryFields?.Any() == true)
                storeCard["secondaryFields"] = req.SecondaryFields
                    .Select(f => new { key = f.Key, label = f.Label, value = f.Value }).ToArray();
            if (req.HeaderFields?.Any() == true)
                storeCard["headerFields"] = req.HeaderFields
                    .Select(f => new { key = f.Key, label = f.Label, value = f.Value }).ToArray();
            if (req.BackFields?.Any() == true)
                storeCard["backFields"] = req.BackFields
                    .Select(f => new { key = f.Key, label = f.Label, value = f.Value }).ToArray();
            // >>> ADI QR-KODUN ÜSTÜNDƏ GÖSTƏRMƏK
            var aux = new List<object>();
            if (req.AuxiliaryFields?.Any() == true)
                aux.AddRange(req.AuxiliaryFields.Select(f => new { key = f.Key, label = f.Label, value = f.Value }));

          
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
                ["foregroundColor"] = NormalizeColor(req.ForegroundColor ?? "#FFFFFF"),
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

            return root;
        }


        // RGB → HEX converter
        private static string NormalizeColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color)) return "#FFFFFF";

            color = color.Trim();

            // Əgər artıq HEX-dirsə
            if (color.StartsWith("#")) return color;

            // rgb(255, 255, 255) formatı
            if (color.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
            {
                var values = color.Replace("rgb(", "").Replace(")", "").Split(',');
                if (values.Length == 3 &&
                    int.TryParse(values[0].Trim(), out var r) &&
                    int.TryParse(values[1].Trim(), out var g) &&
                    int.TryParse(values[2].Trim(), out var b))
                {
                    return $"#{r:X2}{g:X2}{b:X2}";
                }
            }

            return color; // olduğu kimi qaytar
        }

        // RGB → HEX converter
       
    }
}
