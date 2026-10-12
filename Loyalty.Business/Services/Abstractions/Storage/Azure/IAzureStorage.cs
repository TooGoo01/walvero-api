using Microsoft.AspNetCore.Http;

namespace Loyalty.Business.Services.Abstractions.Storage.Azure
{
    public interface IAzureStorage : IStorage
    {
        /// <summary>
        /// Sirr faylları (Apple .p12, Google service account JSON) üçün: konteyner PRIVATE (anonim giriş yox),
        /// blob adı təsadüfi GUID. Qaytarılan URL birbaşa blob URL-idir (CDN deyil) — yalnız storage açarı ilə oxunur.
        /// </summary>
        Task<List<(string fileName, string url)>> UploadPrivateFileAsync(string containerName, IEnumerable<IFormFile> files);
    }
}
