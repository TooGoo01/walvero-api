using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.Business.Services.Abstractions.Storage.Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Loyalty.Business.Services.Implementations.Storage
{
    public class AzureStorage : IAzureStorage
    {
        // 1 il immutable cache — bütün upload-larda random suffix (unique URL) olduğu üçün
        // overwrite riski yoxdur, edge/browser üçün uzun cache təhlükəsizdir.
        private const string LongLivedCacheControl = "public, max-age=31536000, immutable";
        private const string VideoCacheControl = LongLivedCacheControl;

        readonly BlobServiceClient _blobServiceClient;
        BlobContainerClient _blobContainerClient;
        private readonly string? _ffmpegBinaryFolder;
        private readonly ILogger<AzureStorage>? _logger;
        private readonly BlobUrlRewriter? _urlRewriter;

        public AzureStorage(IConfiguration configuration, ILogger<AzureStorage>? logger = null, BlobUrlRewriter? urlRewriter = null)
        {
            _blobServiceClient = new(configuration["Storage:Azure"]);
            _ffmpegBinaryFolder = configuration["FFMpeg:BinaryFolder"];
            _logger = logger;
            _urlRewriter = urlRewriter;
        }

        private string BuildPublicUrl(string containerName, string blobName)
        {
            var raw = $"https://walverostorage.blob.core.windows.net/{containerName}/{blobName}";
            return _urlRewriter?.Rewrite(raw) ?? raw;
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

        public async Task<byte[]?> DownloadAsync(string containerName, string fileName, CancellationToken ct = default)
        {
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            if (!await container.ExistsAsync(ct)) return null;
            var blob = container.GetBlobClient(fileName);
            if (!await blob.ExistsAsync(ct)) return null;
            var resp = await blob.DownloadContentAsync(ct);
            return resp.Value.Content.ToArray();
        }

        public async Task<List<(string fileName, string pathOrContainerName)>> UploadAsync(string pathOrContainerName, IFormFileCollection files)
        {
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(pathOrContainerName);
            await _blobContainerClient.CreateIfNotExistsAsync();
            await _blobContainerClient.SetAccessPolicyAsync(PublicAccessType.Blob);

            var rnd = new Random();

            List<(string fileName, string pathOrContainerName)> datas = new();
            foreach (IFormFile file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file.FileName) + rnd.Next(1, 1000).ToString() + Path.GetExtension(file.FileName);

                BlobClient blobClient = _blobContainerClient.GetBlobClient(name);
                await blobClient.UploadAsync(file.OpenReadStream(), new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = ResolveContentType(file),
                        ContentDisposition = "inline",
                        CacheControl = LongLivedCacheControl,
                    }
                });
                datas.Add((name, BuildPublicUrl(pathOrContainerName, name)));
            }
            return datas;
        }

        private static string ResolveContentType(IFormFile file)
        {
            if (!string.IsNullOrWhiteSpace(file.ContentType) && file.ContentType != "application/octet-stream")
                return file.ContentType;

            return Path.GetExtension(file.FileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".bmp" => "image/bmp",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mov" => "video/quicktime",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };
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
            await _blobContainerClient.SetAccessPolicyAsync(PublicAccessType.Blob);

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

            return (name, BuildPublicUrl(pathOrContainerName, name));
        }

        public async Task<UploadedVideoResult> UploadProcessedVideoAsync(
            string pathOrContainerName,
            IFormFile file)
        {
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(pathOrContainerName);
            await _blobContainerClient.CreateIfNotExistsAsync();
            await _blobContainerClient.SetAccessPolicyAsync(PublicAccessType.Blob);

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".mp4";

            var stem = Path.GetFileNameWithoutExtension(file.FileName)
                + Random.Shared.Next(1000, 9999);
            var videoBlobName = stem + ext;

            var workDir = Path.Combine(Path.GetTempPath(), $"vproc_{Guid.NewGuid():N}");
            Directory.CreateDirectory(workDir);
            var srcPath = Path.Combine(workDir, "src" + ext);
            var fastStartPath = Path.Combine(workDir, "out" + ext);
            var posterPath = Path.Combine(workDir, "poster.jpg");

            string? posterBlobName = null;
            string? posterUrl = null;

            try
            {
                await using (var fs = new FileStream(srcPath, FileMode.Create, FileAccess.Write))
                    await file.CopyToAsync(fs);

                var ffmpeg = ResolveBinary("ffmpeg");
                string videoUploadPath = srcPath;

                if (ffmpeg is not null)
                {
                    var faststartOk = await RunProcessAsync(ffmpeg,
                        new[] { "-y", "-i", srcPath, "-c", "copy", "-movflags", "+faststart", fastStartPath });
                    if (faststartOk && File.Exists(fastStartPath) && new FileInfo(fastStartPath).Length > 0)
                        videoUploadPath = fastStartPath;

                    // Videonun ən ilk frame-i poster kimi istifadə olunur — açılanda
                    // istifadəçi videonun başlanğıc kadrını görür, başqa saniyəni yox.
                    var posterOk = await RunProcessAsync(ffmpeg,
                        new[] { "-y", "-i", srcPath, "-vframes", "1",
                                "-vf", "scale=480:-2", "-q:v", "4", posterPath });

                    if (posterOk && File.Exists(posterPath) && new FileInfo(posterPath).Length > 0)
                    {
                        posterBlobName = stem + ".poster.jpg";
                        var posterClient = _blobContainerClient.GetBlobClient(posterBlobName);
                        await using var posterStream = File.OpenRead(posterPath);
                        await posterClient.UploadAsync(posterStream, new BlobUploadOptions
                        {
                            HttpHeaders = new BlobHttpHeaders
                            {
                                ContentType = "image/jpeg",
                                ContentDisposition = "inline",
                                CacheControl = VideoCacheControl,
                            }
                        });
                        posterUrl = BuildPublicUrl(pathOrContainerName, posterBlobName);
                    }
                }
                else
                {
                    _logger?.LogWarning("ffmpeg tapılmadı (FFMpeg:BinaryFolder='{Folder}') — video raw yüklənir, poster generator olmadı", _ffmpegBinaryFolder);
                }

                var videoClient = _blobContainerClient.GetBlobClient(videoBlobName);
                await using (var videoStream = File.OpenRead(videoUploadPath))
                {
                    await videoClient.UploadAsync(videoStream, new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "video/mp4" : file.ContentType,
                            ContentDisposition = "inline",
                            CacheControl = VideoCacheControl,
                        }
                    });
                }

                var videoUrl = BuildPublicUrl(pathOrContainerName, videoBlobName);
                return new UploadedVideoResult(videoBlobName, videoUrl, posterBlobName, posterUrl);
            }
            finally
            {
                try { Directory.Delete(workDir, recursive: true); } catch { }
            }
        }

        private string? ResolveBinary(string name)
        {
            var exeName = OperatingSystem.IsWindows() ? name + ".exe" : name;

            if (!string.IsNullOrWhiteSpace(_ffmpegBinaryFolder))
            {
                var resolved = Path.IsPathRooted(_ffmpegBinaryFolder)
                    ? _ffmpegBinaryFolder
                    : Path.Combine(AppContext.BaseDirectory, _ffmpegBinaryFolder);
                var candidate = Path.Combine(resolved, exeName);
                if (File.Exists(candidate)) return candidate;
            }

            // PATH-da axtar
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return null;
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    var candidate = Path.Combine(dir, exeName);
                    if (File.Exists(candidate)) return candidate;
                }
                catch { /* invalid path entry — skip */ }
            }
            return null;
        }

        private async Task<bool> RunProcessAsync(string fileName, IEnumerable<string> args, int timeoutMs = 60_000)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                foreach (var a in args) psi.ArgumentList.Add(a);

                using var proc = Process.Start(psi);
                if (proc is null) return false;

                using var cts = new CancellationTokenSource(timeoutMs);
                try
                {
                    await proc.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { proc.Kill(entireProcessTree: true); } catch { }
                    _logger?.LogWarning("Process {Bin} timeout-a düşdü ({Ms} ms)", fileName, timeoutMs);
                    return false;
                }

                if (proc.ExitCode == 0) return true;
                var stderr = await proc.StandardError.ReadToEndAsync();
                _logger?.LogWarning("Process {Bin} exit {Code}: {Err}", fileName, proc.ExitCode, stderr);
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Process {Bin} işə düşmədi", fileName);
                return false;
            }
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

        public async Task<List<(string fileName, string url)>> UploadPrivateFileAsync(string containerName, IEnumerable<IFormFile> files)
        {
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync(PublicAccessType.None);
            // Mövcud (əvvəl public yaradılmış) konteyneri də bağla
            await container.SetAccessPolicyAsync(PublicAccessType.None);

            var result = new List<(string fileName, string url)>();
            foreach (var file in files)
            {
                var name = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
                var blob = container.GetBlobClient(name);
                await using var stream = file.OpenReadStream();
                await blob.UploadAsync(stream, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = "application/octet-stream",
                        CacheControl = "no-store",
                    }
                });
                // Birbaşa blob URL (CDN rewriter YOX) — WalletSecretFetcher bunu storage açarı ilə oxuyur
                result.Add((name, $"https://walverostorage.blob.core.windows.net/{containerName}/{name}"));
            }
            return result;
        }

        public async Task<List<(string fileName, string pathOrContainerName)>> UploadFileAsync(string pathOrContainerName, IEnumerable<IFormFile> files)
        {
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(pathOrContainerName);
            await _blobContainerClient.CreateIfNotExistsAsync();
            await _blobContainerClient.SetAccessPolicyAsync(PublicAccessType.Blob);

            var rnd = new Random();
            List<(string fileName, string pathOrContainerName)> datas = new();

            foreach (IFormFile file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file.FileName) + rnd.Next(1, 1000).ToString() + Path.GetExtension(file.FileName);
                BlobClient blobClient = _blobContainerClient.GetBlobClient(name);
                await blobClient.UploadAsync(file.OpenReadStream(), new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = ResolveContentType(file),
                        ContentDisposition = "inline",
                        CacheControl = LongLivedCacheControl,
                    }
                });
                datas.Add((name, BuildPublicUrl(pathOrContainerName, name)));
            }

            return datas;
        }
    }
}
