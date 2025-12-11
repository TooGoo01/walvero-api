using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IImageAssetService
    {
        Task<ImageUploadResultDto> UploadAsync(
            ImageCategory category,
            IFormFile file,
            string? oldUrl = null,
            CancellationToken ct = default);

        Task<bool> DeleteByUrlAsync(string publicUrl, CancellationToken ct = default);
    }
}
