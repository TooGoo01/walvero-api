using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IRedeemService
    {
        Task<RedeemResultDto> RedeemAsync(RedeemRequestDto request, CancellationToken ct = default);
        Task<RedeemResultDto> ConfirmRedeemAsync(int redeemRequestId, string code, CancellationToken ct = default);
    }
}
