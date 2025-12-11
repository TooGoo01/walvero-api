using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IPasskitService
    {
        Task<ServiceResult> RegisterAsync(
              string deviceLibId, string passTypeId, string serial,
              string authHeader, string pushToken, string? userAgent, CancellationToken ct);

        Task<ServiceResult> UnregisterAsync(
            string deviceLibId, string passTypeId, string serial,
            string authHeader, CancellationToken ct);

        // Data: string (pkpass full path)
        Task<ServiceResult> GetPassPathAsync(string passTypeId, string serial, CancellationToken ct);

        // Data: PassMeta
        Task<ServiceResult> GetPassMetaAsync(string passTypeId, string serial, CancellationToken ct);

        // Data: UpdatedListDto { string LastUpdatedTag; IReadOnlyList<string> Serials; }
        Task<ServiceResult> ListUpdatedAsync(string deviceLibId, string passTypeId, string? sinceTag, CancellationToken ct);
    }
}
