using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IAppleWalletService
    {
        Task<ServiceResult> GeneratePassUrlByCardIdAsync(
         int loyaltyCardId, AppleWalletRuntimeCreds creds, CardTemplateSnapshot snapshot,
         string customerFullName, string barcodeValue, CancellationToken ct = default,
         int lotteryTickets = 0, string? lotteryWinPrize = null, DateTime? lotteryDrawDate = null);
        Task<ServiceResult> RedeemByCardIdAsync(
        int loyaltyCardId,
        CardTemplateSnapshot tpl,
         AppleWalletRuntimeCreds creds,
        decimal currentPoints,
        int currentSteps,
        int pointsDelta,
        CancellationToken ct = default,
        int lotteryTickets = 0,
        string? lotteryWinPrize = null,
        DateTime? lotteryDrawDate = null);

        Task<ServiceResult> PushNotificationToCardAsync(
            int loyaltyCardId,
            string title,
            string body,
            CancellationToken ct = default);
    }
}
