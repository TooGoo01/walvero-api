using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IWalletRedeemer
    {
      
        Task<ServiceResult> RedeemByCardIdAsync(
          int loyaltyCardId,
          CardTemplateSnapshot tpl,
          int currentPoints,
          int currentSteps,
          int pointsDelta,
          CancellationToken ct = default);
    }
}
