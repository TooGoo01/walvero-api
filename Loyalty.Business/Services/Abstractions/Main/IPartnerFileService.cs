using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IPartnerFileService
    {
        Task<ServiceResult> AddRangeAsync(AddPartnerDto partnerDto, int partnerId);
        Task<ServiceResult> UpdateRangeAsync(AddPartnerDto partnerDto, int partnerId);
    }
}
