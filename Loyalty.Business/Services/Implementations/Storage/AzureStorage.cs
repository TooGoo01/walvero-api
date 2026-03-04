using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Loyalty.Business.Services.Abstractions.Storage.Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Loyalty.Business.Services.Implementations.Storage
{
    public class AzureStorage : IAzureStorage
    {
        readonly BlobServiceClient _blobServiceClient;
        BlobContainerClient _blobContainerClient;
        public AzureStorage(IConfiguration configuration)
        {
            _blobServiceClient = new(configuration["Storage:Azure"]);
        }
        public async Task DeleteAsync(string containerName, string fileName)
        {
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = _blobContainerClient.GetBlobClient(fileName);
            await blobClient.DeleteAsync();
        }

        public List<string> GetFiles(string containerName)
        {
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            return _blobContainerClient.GetBlobs().Select(b => b.Name).ToList();
        }

        public bool HasFile(string containerName, string fileName)
        {
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            return _blobContainerClient.GetBlobs().Any(b => b.Name == fileName);
        }

        public async Task<List<(string fileName, string pathOrContainerName)>> UploadAsync(string pathOrContainerName, IFormFileCollection files)
        {
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(pathOrContainerName);
            await _blobContainerClient.CreateIfNotExistsAsync();
            await _blobContainerClient.SetAccessPolicyAsync(PublicAccessType.BlobContainer);

            var rnd = new Random();

            List<(string fileName, string pathOrContainerName)> datas = new();
            foreach (IFormFile file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file.FileName) + rnd.Next(1, 1000).ToString() + Path.GetExtension(file.FileName);
                //string fileNewName = await FileRenameAsync(pathOrContainerName, file.Name, HasFile);

                BlobClient blobClient = _blobContainerClient.GetBlobClient(name);
                await blobClient.UploadAsync(file.OpenReadStream());
                datas.Add((name, $"https://walverostorage.blob.core.windows.net/{pathOrContainerName}/{name}"));
            }
            return datas;
        }

        public async Task<List<(string fileName, string pathOrContainerName)>> UploadFileAsync(string pathOrContainerName, IEnumerable<IFormFile> files)
        {
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(pathOrContainerName);
            await _blobContainerClient.CreateIfNotExistsAsync();
            await _blobContainerClient.SetAccessPolicyAsync(PublicAccessType.BlobContainer);

            var rnd = new Random();
            List<(string fileName, string pathOrContainerName)> datas = new();

            foreach (IFormFile file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file.FileName) + rnd.Next(1, 1000).ToString() + Path.GetExtension(file.FileName);
                BlobClient blobClient = _blobContainerClient.GetBlobClient(name);
                await blobClient.UploadAsync(file.OpenReadStream(), overwrite: true);
                datas.Add((name, $"https://walverostorage.blob.core.windows.net/{pathOrContainerName}/{name}"));
            }

            return datas;
        }
    }
}
