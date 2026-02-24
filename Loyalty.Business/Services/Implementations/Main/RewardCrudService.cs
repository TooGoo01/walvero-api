using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class RewardCrudService : IRewardCrudService
    {
        private readonly IRewardRepository _repo;
        private readonly ILoyaltyProgramRepository _programRepo;
        private readonly IUnitOfWork _uow;

        public RewardCrudService(
            IRewardRepository repo,
            ILoyaltyProgramRepository programRepo,
            IUnitOfWork uow)
        {
            _repo = repo;
            _programRepo = programRepo;
            _uow = uow;
        }

        public async Task<ServiceResult<RewardDto>> CreateAsync(RewardCreateDto dto, int? tenantId, CancellationToken ct = default)
        {
            var program = await _programRepo.GetByIdAsync(dto.LoyaltyProgramId, ct);
            if (program == null)
                return ServiceResult<RewardDto>.Fail("Loyalty proqram tapılmadı.");
            if (tenantId.HasValue && program.TenantId != tenantId.Value)
                return ServiceResult<RewardDto>.Fail("Yetkisiz.");

            var entity = new Reward
            {
                LoyaltyProgramId = dto.LoyaltyProgramId,
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                PointsCost = dto.PointsCost,
                ImageUrl = dto.ImageUrl?.Trim(),
                ExternalProductId = dto.ExternalProductId?.Trim(),
                StockQuantity = dto.StockQuantity,
                IsActive = true,
                RegUserId = 1,
                RegDate = DateTime.UtcNow
            };
            await _repo.AddAsync(entity);
            await _uow.CommitAsync(ct);

            return ServiceResult<RewardDto>.Ok(MapToDto(entity));
        }

        public async Task<ServiceResult<List<RewardDto>>> ListByProgramAsync(int loyaltyProgramId, int? tenantId, CancellationToken ct = default)
        {
            var program = await _programRepo.GetByIdAsync(loyaltyProgramId, ct);
            if (program == null)
                return ServiceResult<List<RewardDto>>.Fail("Loyalty proqram tapılmadı.");
            if (tenantId.HasValue && program.TenantId != tenantId.Value)
                return ServiceResult<List<RewardDto>>.Fail("Yetkisiz.");

            var list = await _repo.GetByProgramIdAsync(loyaltyProgramId, ct);
            var dtos = list.Select(MapToDto).ToList();
            return ServiceResult<List<RewardDto>>.Ok(dtos);
        }

        public async Task<ServiceResult<RewardDto?>> GetByIdAsync(int id, int? tenantId, CancellationToken ct = default)
        {
            var reward = await _repo.GetByIdAsync(id, ct);
            if (reward == null)
                return ServiceResult<RewardDto?>.Ok(null);

            var program = await _programRepo.GetByIdAsync(reward.LoyaltyProgramId, ct);
            if (tenantId.HasValue && program?.TenantId != tenantId.Value)
                return ServiceResult<RewardDto?>.Fail("Yetkisiz.");

            return ServiceResult<RewardDto?>.Ok(MapToDto(reward));
        }

        public async Task<ServiceResult<bool>> UpdateAsync(int id, RewardUpdateDto dto, int? tenantId, CancellationToken ct = default)
        {
            var reward = await _repo.GetByIdAsync(id, ct);
            if (reward == null)
                return ServiceResult<bool>.Fail("Tapılmadı.");

            var program = await _programRepo.GetByIdAsync(reward.LoyaltyProgramId, ct);
            if (tenantId.HasValue && program?.TenantId != tenantId.Value)
                return ServiceResult<bool>.Fail("Yetkisiz.");

            if (dto.Name != null) reward.Name = dto.Name.Trim();
            if (dto.Description != null) reward.Description = dto.Description.Trim();
            if (dto.PointsCost.HasValue) reward.PointsCost = dto.PointsCost.Value;
            if (dto.ImageUrl != null) reward.ImageUrl = dto.ImageUrl.Trim();
            if (dto.ExternalProductId != null) reward.ExternalProductId = dto.ExternalProductId.Trim();
            if (dto.IsActive.HasValue) reward.IsActive = dto.IsActive.Value;
            if (dto.StockQuantity.HasValue) reward.StockQuantity = dto.StockQuantity.Value;

            _repo.Update(reward);
            await _uow.CommitAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(int id, int? tenantId, CancellationToken ct = default)
        {
            var reward = await _repo.GetByIdAsync(id, ct);
            if (reward == null)
                return ServiceResult<bool>.Fail("Tapılmadı.");

            var program = await _programRepo.GetByIdAsync(reward.LoyaltyProgramId, ct);
            if (tenantId.HasValue && program?.TenantId != tenantId.Value)
                return ServiceResult<bool>.Fail("Yetkisiz.");

            reward.IsActive = false;
            _repo.Update(reward);
            await _uow.CommitAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }

        private static RewardDto MapToDto(Reward r) => new()
        {
            Id = r.Id,
            LoyaltyProgramId = r.LoyaltyProgramId,
            Name = r.Name,
            Description = r.Description,
            PointsCost = r.PointsCost,
            ImageUrl = r.ImageUrl,
            ExternalProductId = r.ExternalProductId,
            IsActive = r.IsActive,
            StockQuantity = r.StockQuantity
        };
    }
}
