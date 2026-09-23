using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ILoyaltyProgramService
    {
        Task<ServiceResult> Add(AddLoyaltyProgramDto Dto);
        Task<ServiceResult> AddOrUpdateAsync(AddLoyaltyProgramDto dto, CancellationToken ct = default);
        Task<ServiceResult> Get(int id);
        Task<ServiceResult> GetAll();
        Task<ServiceResult> GetActive();
        /// <summary>Yalnız verilən tenant-ın aktiv proqramları (tenant admin üçün).</summary>
        Task<ServiceResult> GetActiveByTenantAsync(int tenantId);
        Task<ServiceResult> GetLast();
        Task<ServiceResult> Update(AddLoyaltyProgramDto dto, int id);
        Task<ServiceResult> Delete(int id);
        Task<ServiceResult> UpdateHappyHoursAsync(int programId, UpdateHappyHoursDto dto, CancellationToken ct = default);
    }
}
