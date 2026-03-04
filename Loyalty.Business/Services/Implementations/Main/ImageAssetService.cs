using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Enums;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class ImageAssetService : IImageAssetService
    {
        private readonly ITenantProvider _tp;
        private readonly IStorage _storage;
        private readonly AssetOptions _opt;

        public ImageAssetService(
            ITenantProvider tp,
            IStorage storage,
            IOptions<AssetOptions> opt)
        {
            _tp = tp;
            _storage = storage;
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

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType($"x{ext}", out _))
                throw new InvalidOperationException("Unknown content type.");

            // Container: tenant-{id}-{category}
            var categoryFolder = GetCategoryFolder(category);
            var container = $"tenant-{tenantId}-{categoryFolder}";

            // Azure Blob Storage-a yüklə
            var uploaded = await _storage.UploadFileAsync(container, new[] { file });
            if (uploaded.Count == 0) throw new InvalidOperationException("Upload failed.");

            var (fileName, publicUrl) = uploaded[0];

            // Köhnəni sil
            if (!string.IsNullOrWhiteSpace(oldUrl))
                TryDeleteBlob(oldUrl);

            return new ImageUploadResultDto { Url = publicUrl, FileName = fileName };
        }

        public Task<bool> DeleteByUrlAsync(string publicUrl, CancellationToken ct = default)
            => Task.FromResult(TryDeleteBlob(publicUrl));

        private static string GetCategoryFolder(ImageCategory c) => c switch
        {
            ImageCategory.Branding => "branding",
            ImageCategory.Program => "program",
            ImageCategory.Customer => "customer",
            ImageCategory.Product => "product",
            _ => "other"
        };

        private bool TryDeleteBlob(string url)
        {
            try
            {
                if (!url.Contains("walverostorage.blob.core.windows.net")) return false;

                var uri = new Uri(url, UriKind.Absolute);
                var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
                if (segments.Length < 2) return false;

                var container = segments[0];
                var fileName = segments[1];
                _storage.DeleteAsync(container, fileName).GetAwaiter().GetResult();
                return true;
            }
            catch { return false; }
        }
    }
}
