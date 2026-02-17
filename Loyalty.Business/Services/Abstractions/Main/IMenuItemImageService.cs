using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IMenuItemImageService
    {
        Task<ServiceResult> AddRangeAsync(AddMenuItemDto Dto, int Id);
        Task<ServiceResult> UpdateRangeAsync(AddMenuItemDto Dto, int Id);
    }
}
