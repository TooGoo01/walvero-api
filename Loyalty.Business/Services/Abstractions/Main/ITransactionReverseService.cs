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
    public interface ITransactionReverseService
    {
        Task<ServiceResult<ReverseTransactionResponseDto>> ReverseTransactionAsync(ReverseTransactionDto dto, CancellationToken ct = default);
        Task<ServiceResult<ReverseTransactionResponseDto>> ReverseLastTransactionByCardAsync(string cardNumber, string? reason, CancellationToken ct = default);

        /// <summary>
        /// FlaggedForReview transaction-ı admin tərəfindən təsdiqlə — balansa tətbiq et + status Applied.
        /// callerBranchId null isə (Tenant Admin) hamı; əks halda yalnız öz branch-ının tx-ləri qəbul.
        /// </summary>
        Task<ServiceResult> ApproveFlaggedAsync(int transactionId, int? callerBranchId, CancellationToken ct = default);

        /// <summary>
        /// FlaggedForReview transaction-ı rədd et — status Reversed, balans dəyişməz (artıq dəyişməyib).
        /// </summary>
        Task<ServiceResult> RejectFlaggedAsync(int transactionId, int? callerBranchId, string? reason, CancellationToken ct = default);
    }
}

