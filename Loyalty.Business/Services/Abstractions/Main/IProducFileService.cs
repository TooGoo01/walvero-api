using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IProducFileService
    {
        Task<ServiceResult> AddRangeAsync(AddProductDto Dto, int Id);
        Task<ServiceResult> UpdateRangeAsync(AddProductDto Dto, int Id);
    }
}
