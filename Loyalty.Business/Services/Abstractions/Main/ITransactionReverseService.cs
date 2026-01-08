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
    }
}

