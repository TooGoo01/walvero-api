using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Loyalty.Business.Services.Abstractions.Storage.Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

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
            if (!await _blobContainerClient.ExistsAsync()) return;
            BlobClient blobClient = _blobContainerClient.GetBlobClient(fileName);
            await blobClient.DeleteIfExistsAsync();
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

        public async Task<(string fileName, string pathOrContainerName)> UploadVideoHlsAsync(
            string pathOrContainerName,
            IFormFile file)
        {
            // ffmpeg pipeline (HLS / faststart) müvəqqəti deaktiv edilib — Basic B1 plan-da
            // synchronous encoding reverse proxy timeout-una düşürdü.
            // Hazırda video olduğu kimi blob-a yüklənir. Sürətli açılma üçün gələcəkdə:
            //   1) Plan upgrade (S1+) sonra HLS-i bərpa et, və ya
            //   2) Asynchronous job pattern (background queue) tətbiq et.
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(pathOrContainerName);
            await _blobContainerClient.CreateIfNotExistsAsync();
            await _blobContainerClient.SetAccessPolicyAsync(PublicAccessType.BlobContainer);

            var name = Path.GetFileNameWithoutExtension(file.FileName)
                + Random.Shared.Next(1000, 9999) + Path.GetExtension(file.FileName);
            var blobClient = _blobContainerClient.GetBlobClient(name);

            await using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "video/mp4" : file.ContentType,
                    ContentDisposition = "inline",
                    CacheControl = "public, max-age=31536000, immutable",
                }
            });

            return (name, $"https://walverostorage.blob.core.windows.net/{pathOrContainerName}/{name}");
        }

        /// <summary>
        /// HLS output folder-i (master.m3u8 + variant playlist-lər + .ts segmentlər) blob storage-a yükləyir.
        /// `videoId` blob folder prefix-idir, daxili strukturu (v0/, v1/) qoruyur.
        /// </summary>
        private async Task UploadHlsFolderAsync(string localFolder, string videoId)
        {
            foreach (var localPath in Directory.EnumerateFiles(localFolder, "*", SearchOption.AllDirectories))
            {
                // Relative path-i blob path-a çevir (Windows backslash → forward slash)
                var rel = Path.GetRelativePath(localFolder, localPath).Replace('\\', '/');
                var blobPath = $"{videoId}/{rel}";
                var blobClient = _blobContainerClient.GetBlobClient(blobPath);

                var ext = Path.GetExtension(localPath).ToLowerInvariant();
                var contentType = ext switch
                {
                    ".m3u8" => "application/vnd.apple.mpegurl",
                    ".ts" => "video/mp2t",
                    _ => "application/octet-stream",
                };

                await using var fs = new FileStream(localPath, FileMode.Open, FileAccess.Read);
                await blobClient.UploadAsync(fs, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = contentType,
                        ContentDisposition = "inline",
                        // Segment-lər və playlist-lər immutable — videoId GUID-dir, eyni URL təkrar olunmaz
                        CacheControl = "public, max-age=31536000, immutable",
                    }
                });
            }
        }

        /// <summary>
        /// ffprobe ilə video müddətini saniyələrdə qaytarır.
        /// `ffprobe` adətən `ffmpeg` paketi ilə birlikdə qurulur (Linux: `apt install ffmpeg`).
        /// </summary>
        private static async Task<double> GetVideoDurationSecondsAsync(string filePath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                ArgumentList =
                {
                    "-v", "error",
                    "-show_entries", "format=duration",
                    "-of", "default=noprint_wrappers=1:nokey=1",
                    filePath,
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("ffprobe-i başlatmaq mümkün olmadı");
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                var stderr = await proc.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"ffprobe uğursuz oldu (exit {proc.ExitCode}): {stderr}");
            }

            // Output format: "59.987654\n" — invariant culture ilə parse et
            if (double.TryParse(stdout.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            {
                return seconds;
            }
            throw new InvalidOperationException($"Video müddəti parse oluna bilmədi: '{stdout}'");
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
                await blobClient.UploadAsync(file.OpenReadStream(), new Azure.Storage.Blobs.Models.BlobUploadOptions
                {
                    HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
                    {
                        ContentType = file.ContentType ?? "application/octet-stream",
                        ContentDisposition = "inline"
                    }
                });
                datas.Add((name, $"https://walverostorage.blob.core.windows.net/{pathOrContainerName}/{name}"));
            }

            return datas;
        }
    }
}
