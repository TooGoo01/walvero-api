using Loyalty.DataAccess.Entities.Main;
using Microsoft.AspNetCore.Http;

namespace Loyalty.Business.Services.Abstractions.Storage
{
    public interface IStorage
    {
        Task<List<(string fileName, string pathOrContainerName)>> UploadAsync(string pathOrContainerName, IFormFileCollection files);
        Task<List<(string fileName, string pathOrContainerName)>> UploadFileAsync(
              string pathOrContainerName,
              IEnumerable<IFormFile> files);

        /// <summary>
        /// Video-nu ffmpeg ilə HLS multi-bitrate formatına çevirir (360p + 720p variantları,
        /// 4-saniyəlik .ts segmentləri, master.m3u8 playlist) və storage-a yükləyir.
        /// Native player (ExoPlayer/AVPlayer) ilk segmenti yükləyən kimi play başladır —
        /// YouTube/Instagram-tipli demək olar ani açılma. Şəbəkə zəifdirsə avtomatik 360p-yə düşür.
        /// Qaytarılan URL master.m3u8-ə işarə edir.
        /// Müddət limiti 120 saniyədir; daha uzun video atılarsa exception throw olunur.
        /// </summary>
        Task<(string fileName, string pathOrContainerName)> UploadVideoHlsAsync(
              string pathOrContainerName,
              IFormFile file);

        /// <summary>
        /// Video-nu sürətli açılma üçün hazırlayıb yükləyir:
        ///  1) ffmpeg `-c copy -movflags +faststart` — moov atom faylın başına köçürülür,
        ///     player tam download gözləməyib ilk neçə yüz KB yüklənən kimi play başladır.
        ///  2) ffmpeg ilə ~1-ci saniyədən poster JPEG generator olunur.
        ///  3) Hər iki blob `Cache-Control: public, max-age=31536000, immutable`,
        ///     `Content-Disposition: inline`, dəqiq `Content-Type` ilə yüklənir.
        /// ffmpeg yoxdursa video-nu raw kimi yükləyir (yenə də cache header-ları ilə),
        /// posterUrl null qayıdır — graceful degrade.
        /// </summary>
        Task<UploadedVideoResult> UploadProcessedVideoAsync(
              string pathOrContainerName,
              IFormFile file);

        Task DeleteAsync(string pathOrContainerName, string fileName);
        List<string> GetFiles(string pathOrContainerName);
        bool HasFile(string pathOrContainerName, string fileName);
        Task<byte[]?> DownloadAsync(string pathOrContainerName, string fileName, CancellationToken ct = default);
    }
}
