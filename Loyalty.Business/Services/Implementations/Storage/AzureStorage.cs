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
            _blobContainerClient = _blobServiceClient.GetBlobContainerClient(pathOrContainerName);
            await _blobContainerClient.CreateIfNotExistsAsync();
            await _blobContainerClient.SetAccessPolicyAsync(PublicAccessType.BlobContainer);

            // Hər video üçün ayrıca temp folder + ayrıca blob folder (variant playlists + segments).
            var videoId = $"hls-{Guid.NewGuid():N}";
            var tempInput = Path.Combine(Path.GetTempPath(), $"vin_{videoId}{Path.GetExtension(file.FileName)}");
            var tempDir = Path.Combine(Path.GetTempPath(), videoId);
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(Path.Combine(tempDir, "v0"));
            Directory.CreateDirectory(Path.Combine(tempDir, "v1"));

            try
            {
                // 1) Upload-edilmiş faylı temp-də saxla
                await using (var fs = new FileStream(tempInput, FileMode.Create, FileAccess.Write))
                {
                    await file.CopyToAsync(fs);
                }

                // 2) Müddət yoxlaması — 2 dəqiqədən uzun olmamalıdır
                var duration = await GetVideoDurationSecondsAsync(tempInput);
                if (duration > 120.5) // 0.5s tolerance encoding rounding üçün
                {
                    throw new InvalidOperationException(
                        $"Video 2 dəqiqədən uzun ola bilməz. Yüklənən video {duration:F1} saniyədir.");
                }

                // 3) ffmpeg multi-bitrate HLS encode (360p + 720p variants, 4s segments).
                //    `-preset ultrafast` 3-5× sürətli encoding (fayl 10-15% böyük olur, key.fərqi yox).
                //    `-threads 0` bütün CPU core-ları istifadə et.
                //    `-keyint_min` və `-g` GOP-u 4s ilə hizalayır → segment seçimi düzgün olur.
                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    ArgumentList =
                    {
                        "-y",
                        "-hide_banner",
                        "-loglevel", "warning",
                        "-i", tempInput,
                        "-filter_complex",
                        "[0:v]split=2[v1][v2];" +
                        "[v1]scale=w=640:h=360:force_original_aspect_ratio=decrease[v360];" +
                        "[v2]scale=w=1280:h=720:force_original_aspect_ratio=decrease[v720]",
                        "-map", "[v360]",
                        "-c:v:0", "libx264", "-profile:v:0", "main",
                        "-preset:v:0", "ultrafast",
                        "-b:v:0", "800k", "-maxrate:v:0", "900k", "-bufsize:v:0", "1200k",
                        "-g", "96", "-keyint_min", "96", "-sc_threshold", "0",
                        "-map", "[v720]",
                        "-c:v:1", "libx264", "-profile:v:1", "main",
                        "-preset:v:1", "ultrafast",
                        "-b:v:1", "2000k", "-maxrate:v:1", "2200k", "-bufsize:v:1", "3000k",
                        "-g", "96", "-keyint_min", "96", "-sc_threshold", "0",
                        "-map", "a:0?", "-c:a:0", "aac", "-b:a:0", "96k", "-ac:a:0", "2",
                        "-map", "a:0?", "-c:a:1", "aac", "-b:a:1", "128k", "-ac:a:1", "2",
                        "-var_stream_map", "v:0,a:0 v:1,a:1",
                        "-master_pl_name", "master.m3u8",
                        "-hls_time", "4",
                        "-hls_playlist_type", "vod",
                        "-hls_segment_filename", Path.Combine(tempDir, "v%v", "seg_%03d.ts"),
                        "-threads", "0",
                        "-f", "hls",
                        Path.Combine(tempDir, "v%v", "playlist.m3u8"),
                    },
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                Process? proc;
                try
                {
                    proc = Process.Start(psi);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    // ffmpeg server-də quraşdırılmayıb (Linux: `apt install ffmpeg`).
                    throw new InvalidOperationException(
                        "Server-də ffmpeg tapılmadı. Linux-da quraşdırın: `sudo apt install -y ffmpeg`. " +
                        $"(detail: {ex.Message})");
                }
                if (proc == null)
                    throw new InvalidOperationException("ffmpeg prosesi başlatmaq mümkün olmadı");

                using (proc)
                {
                    // ffmpeg STDERR-ə yazır (progress, errors). Tamamlanmasını gözləyərkən
                    // 5 dəqiqəlik timeout-la qoruyuruq ki, server hang olmasın.
                    var timeout = TimeSpan.FromMinutes(5);
                    using var cts = new CancellationTokenSource(timeout);
                    try
                    {
                        await proc.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try { proc.Kill(entireProcessTree: true); } catch { }
                        throw new InvalidOperationException(
                            $"ffmpeg encoding 5 dəqiqədən uzun çəkdi və dayandırıldı. " +
                            "Server CPU yetərsiz ola bilər və ya video çox böyükdür.");
                    }

                    if (proc.ExitCode != 0)
                    {
                        var stderr = await proc.StandardError.ReadToEndAsync();
                        throw new InvalidOperationException($"ffmpeg uğursuz oldu (exit {proc.ExitCode}): {stderr}");
                    }
                }

                // 4) tempDir altında yaranan bütün faylları blob-a yüklə.
                //    Folder strukturu: master.m3u8, v0/playlist.m3u8 + seg_*.ts, v1/...
                await UploadHlsFolderAsync(tempDir, videoId);

                var masterUrl = $"https://walverostorage.blob.core.windows.net/{pathOrContainerName}/{videoId}/master.m3u8";
                return (videoId, masterUrl);
            }
            finally
            {
                try { if (File.Exists(tempInput)) File.Delete(tempInput); } catch { }
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); } catch { }
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
