using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.DataAccess.Entities.Main;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using File = System.IO.File;

namespace Loyalty.Business.Services.Implementations.Storage
{
    public class FileStorage : IStorage
    {
        private readonly IHostingEnvironment _webHostEnvironment;
        //private readonly IWebHostEnvironment _webHostEnvironment;

        public FileStorage(IHostingEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }
        public Task<byte[]?> DownloadAsync(string pathOrContainerName, string fileName, CancellationToken ct = default)
        {
            // FileStorage prod-da istifadə olunmur (yalnız local dev). Sadə impl.
            var rootPath = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot", pathOrContainerName);
            var fullPath = Path.Combine(rootPath, fileName);
            if (!File.Exists(fullPath)) return Task.FromResult<byte[]?>(null);
            return Task.FromResult<byte[]?>(File.ReadAllBytes(fullPath));
        }

        public Task DeleteAsync(string pathOrContainerName, string fileName)
        {
            if (string.IsNullOrWhiteSpace(pathOrContainerName))
                throw new ArgumentException("pathOrContainerName is required.", nameof(pathOrContainerName));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("fileName is required.", nameof(fileName));

            // Base folder (same root as in UploadFileAsync)
            var baseDir = Path.GetFullPath(Path.Combine("wwwroot", "resource"));

            // Build absolute paths safely
            var targetDir = Path.GetFullPath(Path.Combine(baseDir, pathOrContainerName));
            var targetFile = Path.GetFullPath(Path.Combine(targetDir, fileName));

            // Prevent escaping the base directory (path traversal)
            var cmp = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!targetDir.StartsWith(baseDir, cmp) || !targetFile.StartsWith(baseDir, cmp))
                throw new InvalidOperationException("Invalid path detected.");

            // If file exists, delete it
            if (File.Exists(targetFile))
            {
                // remove read-only attribute if any
                var attrs = File.GetAttributes(targetFile);
                if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    File.SetAttributes(targetFile, attrs & ~FileAttributes.ReadOnly);

                File.Delete(targetFile);
            }

            // Optionally clean up the (now) empty directory
            if (Directory.Exists(targetDir) &&
                !Directory.EnumerateFileSystemEntries(targetDir).Any())
            {
                Directory.Delete(targetDir, recursive: false);
            }

            return Task.CompletedTask;
        }


        public List<string> GetFiles(string pathOrContainerName)
        {
            //_blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            ///return _blobContainerClient.GetBlobs().Select(b => b.Name).ToList();
            //_webHostEnvironment= 
            throw new NotImplementedException();
        }
        public async Task<List<(string fileName, string pathOrContainerName)>> UploadFileAsync(
    string pathOrContainerName,
    IEnumerable<IFormFile> files)
        {
            var datas = new List<(string fileName, string pathOrContainerName)>();
            var upload = Path.Combine("wwwroot", "resource", pathOrContainerName);
            Directory.CreateDirectory(upload);

            foreach (var file in files)
            {
                var fullPath = Path.Combine(upload, file.FileName);
                using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: false);
                await file.CopyToAsync(fs);
                await fs.FlushAsync();
                datas.Add((file.FileName, $"/resource/{pathOrContainerName}/{file.FileName}"));
            }
            return datas;
        }
        public bool HasFile(string pathOrContainerName, string fileName)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Local dev fallback — ffmpeg quraşdırılmadığı mühitlərdə video-nu olduğu kimi yazır
        /// (HLS yaratmır). Production-da AzureStorage istifadə olunur.
        /// </summary>
        public async Task<(string fileName, string pathOrContainerName)> UploadVideoHlsAsync(
            string pathOrContainerName,
            IFormFile file)
        {
            var upload = Path.Combine("wwwroot", "resource", pathOrContainerName);
            Directory.CreateDirectory(upload);
            var name = Path.GetFileNameWithoutExtension(file.FileName)
                + Random.Shared.Next(1000, 9999) + ".mp4";
            var fullPath = Path.Combine(upload, name);
            await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await file.CopyToAsync(fs);
            return (name, $"/resource/{pathOrContainerName}/{name}");
        }

        public async Task<List<(string fileName, string pathOrContainerName)>> UploadAsync(string pathOrContainerName, IFormFileCollection files)
        {
            List<(string fileName, string pathOrContainerName)> datas = new();

            string upload = Path.Combine($"wwwroot/resurce/{pathOrContainerName}");
            if (!Directory.Exists(upload))
            {
                Directory.CreateDirectory(upload);
            }
            Random r = new Random();

            foreach (IFormFile file in files)
            {
                string fullPath = Path.Combine(upload, $"{file.FileName}");
                using FileStream fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: false);
                await file.CopyToAsync(fileStream);
                await fileStream.FlushAsync();
                datas.Add((file.FileName, $@"{upload.Remove(0, 8)}/{file.FileName}"));
            }
            return datas;
        }

       
    }
}
