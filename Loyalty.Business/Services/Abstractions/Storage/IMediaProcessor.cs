using Microsoft.AspNetCore.Http;

namespace Loyalty.Business.Services.Abstractions.Storage
{
    public interface IMediaProcessor
    {
        /// <summary>Converts an image to WebP format with quality optimization. Returns a new IFormFile.</summary>
        Task<IFormFile> ConvertImageToWebpAsync(IFormFile file, int quality = 80);

        /// <summary>Returns true if the file is an image type.</summary>
        bool IsImage(IFormFile file);
    }
}
