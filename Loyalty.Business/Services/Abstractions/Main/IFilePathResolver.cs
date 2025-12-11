using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IFilePathResolver
    {
        /// <summary>
        /// Verilən path və ya URL-i həll edir:
        /// - Əgər http/https URL-dirsə → yükləyir, wwwroot/passes/downloads altına yazır, local path qaytarır
        /// - Əgər nisbi və ya absolute fayl yoludursa → local path kimi yoxlayıb qaytarır
        /// </summary>
        Task<string> ResolvePathOrDownloadAsync(
            string pathOrUrl,
            string prefix,
            string defaultExt,
            CancellationToken ct = default);
    }
}
