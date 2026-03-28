using Loyalty.Business.Services.Abstractions.Storage;
using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;

namespace Loyalty.Business.Services.Implementations.Storage
{
    public class MediaProcessor : IMediaProcessor
    {
        private static readonly HashSet<string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp", "image/tiff", "image/webp"
        };

        public bool IsImage(IFormFile file)
        {
            return ImageMimeTypes.Contains(file.ContentType);
        }

        public async Task<IFormFile> ConvertImageToWebpAsync(IFormFile file, int quality = 80)
        {
            if (file.ContentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
                return file;

            try
            {
                using var inputStream = file.OpenReadStream();
                using var image = await Image.LoadAsync(inputStream);

                var ms = new MemoryStream();
                var encoder = new WebpEncoder { Quality = quality };
                await image.SaveAsync(ms, encoder);
                ms.Position = 0;

                var newName = Path.GetFileNameWithoutExtension(file.FileName) + ".webp";
                return new FormFile(ms, 0, ms.Length, file.Name, newName)
                {
                    Headers = file.Headers,
                    ContentType = "image/webp"
                };
            }
            catch
            {
                return file;
            }
        }
    }
}
