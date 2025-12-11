using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Enums;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class ImageAssetService : IImageAssetService
    {
        private readonly ITenantProvider _tp;
        private readonly IHostingEnvironment _env;
        private readonly IHttpContextAccessor _http;
        private readonly AssetOptions _opt;

        public ImageAssetService(
            ITenantProvider tp,
            IHostingEnvironment env,
            IHttpContextAccessor http,
            IOptions<AssetOptions> opt)
        {
            _tp = tp;
            _env = env;
            _http = http;
            _opt = opt.Value;
        }

        public async Task<ImageUploadResultDto> UploadAsync(
            ImageCategory category,
            IFormFile file,
            string? oldUrl = null,
            CancellationToken ct = default)
        {
            var tenantId = _tp.CurrentTenantId ?? throw new UnauthorizedAccessException("Tenant not resolved.");
            if (file is null || file.Length == 0) throw new InvalidOperationException("Empty file.");
            if (file.Length > _opt.MaxBytes) throw new InvalidOperationException($"File too large. Max {_opt.MaxBytes / 1024 / 1024} MB.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_opt.AllowedExt.Contains(ext)) throw new InvalidOperationException("Unsupported file type.");

            // Sadə content-type yoxlaması (magic-bytes yoxlamasını istəsən əlavə edərsən)
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType($"x{ext}", out _))
                throw new InvalidOperationException("Unknown content type.");

            // Fayl adı: təhlükəsiz + unikaldır
            var safeBase = Slugify(Path.GetFileNameWithoutExtension(file.FileName));
            if (string.IsNullOrWhiteSpace(safeBase)) safeBase = "img";
            var unique = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
            var finalName = $"{safeBase}_{unique}{ext}";

            // Qovluq yolu: wwwroot/uploads/tenant-{id}/{category}/
            var categoryFolder = GetCategoryFolder(category);
            var relativeDir = Path.Combine(_opt.RootFolder, $"tenant-{tenantId}", categoryFolder);
            var absDir = Path.Combine(_env.WebRootPath, relativeDir);
            Directory.CreateDirectory(absDir);

            // Yaz
            var absPath = Path.Combine(absDir, finalName);
            using (var stream = File.Create(absPath))
                await file.CopyToAsync(stream, ct);

            // Public URL
            var ctx = _http.HttpContext!;
            var baseUri = $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.PathBase}";
            var publicUrl = $"{baseUri}/{relativeDir.Replace("\\", "/")}/{Uri.EscapeDataString(finalName)}";

            // Köhnəni sil (yalnız bizim uploads içindədirsə)
            if (!string.IsNullOrWhiteSpace(oldUrl)) TryDeleteLocal(oldUrl);

            return new ImageUploadResultDto { Url = publicUrl, FileName = finalName };
        }

        public Task<bool> DeleteByUrlAsync(string publicUrl, CancellationToken ct = default)
            => Task.FromResult(TryDeleteLocal(publicUrl));

        // Helpers
        private static string GetCategoryFolder(ImageCategory c) => c switch
        {
            ImageCategory.Branding => "branding",
            ImageCategory.Program => "program",
            ImageCategory.Customer => "customer",
            ImageCategory.Product => "product",
            _ => "other"
        };

        private static string Slugify(string input)
        {
            input = input.ToLowerInvariant();
            input = Regex.Replace(input, @"\s+", "-");
            input = Regex.Replace(input, @"[^a-z0-9\-_]", "");
            return input.Trim('-', '_');
        }

        private bool TryDeleteLocal(string url)
        {
            try
            {
                var local = MapUrlToLocal(url);
                if (local is null) return false;

                // Təhlükəsizlik: yalnız uploads kökü içindəki faylı sil
                var uploadsRoot = Path.Combine(_env.WebRootPath, _opt.RootFolder);
                if (!Path.GetFullPath(local).StartsWith(Path.GetFullPath(uploadsRoot), StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!File.Exists(local)) return false;
                File.Delete(local);
                return true;
            }
            catch { return false; }
        }

        private string? MapUrlToLocal(string url)
        {
            try
            {
                var uri = new Uri(url, UriKind.Absolute);
                var localRel = uri.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                return Path.Combine(_env.WebRootPath, localRel);
            }
            catch { return null; }
        }
    }
}
