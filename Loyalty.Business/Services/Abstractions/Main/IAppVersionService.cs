
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IAppVersionService
    {
        Task<ServiceResult> Add(AddAppVersion Dto);
        Task<ServiceResult> Get(int id);
        Task<ServiceResult> GetAll();
        Task<ServiceResult> GetActive();
        Task<ServiceResult> Check(string plaform,string currentVersion);
        Task<ServiceResult> Update(AddAppVersion dto, int id);
        Task<ServiceResult> Delete(int id);
        Task<ServiceResult> Active(int id);
        Task<ServiceResult> Deactive(int id);
    }
}
