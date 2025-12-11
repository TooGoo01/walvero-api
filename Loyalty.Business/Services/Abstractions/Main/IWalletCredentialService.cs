using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IWalletCredentialService
    {
        Task<ServiceResult> Add(AddTenantBrandingDto Dto);
        Task<ServiceResult> AddOrUpdateAsync(AddWalletCredentialDto dto, CancellationToken ct = default);
        Task<ServiceResult> Get(int id);
        Task<ServiceResult> GetByProgram(int id,WalletPlatform programId);
        Task<ServiceResult> GetAll();
        Task<ServiceResult> GetLast();
        Task<ServiceResult> Update(AddWalletCredentialDto dto, int id);
        Task<ServiceResult> Delete(int id);
    }
}
