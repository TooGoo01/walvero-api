using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IPosSessionService
    {
        Task<ServiceResult<ScannerSessionResponseDto>> StartSessionAsync(StartScannerSessionDto dto, CancellationToken ct = default);
        Task<ServiceResult<LinkSessionResponseDto>> LinkSessionAsync(LinkSessionDto dto, CancellationToken ct = default);
    }
}

