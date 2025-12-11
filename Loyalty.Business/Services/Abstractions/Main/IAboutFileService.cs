using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IAboutFileService
    {
        Task<ServiceResult> AddRangeAsync(AddAboutDto aboutDto, int aboutId);
        Task<ServiceResult> UpdateRangeAsync(AddAboutDto aboutDto, int aboutId);
    }
}
