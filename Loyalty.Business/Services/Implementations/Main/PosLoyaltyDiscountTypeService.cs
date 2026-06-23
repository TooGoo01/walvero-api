using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class PosLoyaltyDiscountTypeService : IPosLoyaltyDiscountTypeService
    {
        private readonly IPosLoyaltyDiscountTypeRepository _repo;
        private readonly ITenantIntegrationRepository _tenantIntegrationRepo;
        private readonly IUnitOfWork _uow;

        public PosLoyaltyDiscountTypeService(
            IPosLoyaltyDiscountTypeRepository repo,
            ITenantIntegrationRepository tenantIntegrationRepo,
            IUnitOfWork uow)
        {
            _repo = repo;
            _tenantIntegrationRepo = tenantIntegrationRepo;
            _uow = uow;
        }

        public async Task<ServiceResult<PosLoyaltyDiscountTypeDto>> CreateAsync(PosLoyaltyDiscountTypeCreateDto dto, int? tenantId, CancellationToken ct = default)
        {
            var ti = await _tenantIntegrationRepo.GetByIdAsync(dto.TenantIntegrationId, ct);
            if (ti == null) return ServiceResult<PosLoyaltyDiscountTypeDto>.Fail("TenantIntegration tapılmadı.");
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<PosLoyaltyDiscountTypeDto>.Fail("Yetkisiz.");

            var externalId = dto.ExternalDiscountTypeId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(externalId))
                return ServiceResult<PosLoyaltyDiscountTypeDto>.Fail("ExternalDiscountTypeId tələb olunur.");

            var existing = (await _repo.GetAllAsync(r =>
                r.TenantIntegrationId == dto.TenantIntegrationId
                && r.ExternalDiscountTypeId == externalId)).FirstOrDefault();
            if (existing != null)
                return ServiceResult<PosLoyaltyDiscountTypeDto>.Fail("Bu ExternalDiscountTypeId artıq mövcuddur.");

            var entity = new PosLoyaltyDiscountType
            {
                TenantIntegrationId = dto.TenantIntegrationId,
                ExternalDiscountTypeId = externalId,
                DiscountName = dto.DiscountName?.Trim(),
                IsActive = true,
                RegUserId = 1,
                RegDate = DateTime.UtcNow
            };
            await _repo.AddAsync(entity);
            _uow.Commit();

            var saved = await _repo.GetByIdAsync(entity.Id, ct);
            if (saved == null) return ServiceResult<PosLoyaltyDiscountTypeDto>.Fail("Yaradıla bilmədi.");

            return ServiceResult<PosLoyaltyDiscountTypeDto>.Ok(Map(saved));
        }

        public async Task<ServiceResult<List<PosLoyaltyDiscountTypeDto>>> ListByTenantIntegrationAsync(int tenantIntegrationId, int? tenantId, CancellationToken ct = default)
        {
            var ti = await _tenantIntegrationRepo.GetByIdAsync(tenantIntegrationId, ct);
            if (ti == null) return ServiceResult<List<PosLoyaltyDiscountTypeDto>>.Fail("TenantIntegration tapılmadı.");
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<List<PosLoyaltyDiscountTypeDto>>.Fail("Yetkisiz.");

            var list = await _repo.GetByTenantIntegrationIdAsync(tenantIntegrationId, ct);
            var dtos = list.Select(Map).ToList();
            return ServiceResult<List<PosLoyaltyDiscountTypeDto>>.Ok(dtos);
        }

        public async Task<ServiceResult<bool>> UpdateAsync(int id, PosLoyaltyDiscountTypeUpdateDto dto, int? tenantId, CancellationToken ct = default)
        {
            var r = await _repo.GetByIdAsync(id, ct);
            if (r == null) return ServiceResult<bool>.Fail("Tapılmadı.");

            var ti = await _tenantIntegrationRepo.GetByIdAsync(r.TenantIntegrationId, ct);
            if (ti == null || (tenantId.HasValue && ti.TenantId != tenantId.Value))
                return ServiceResult<bool>.Fail("Yetkisiz.");

            if (dto.ExternalDiscountTypeId != null)
            {
                var trimmed = dto.ExternalDiscountTypeId.Trim();
                if (!string.Equals(trimmed, r.ExternalDiscountTypeId, StringComparison.OrdinalIgnoreCase))
                {
                    var dup = (await _repo.GetAllAsync(x =>
                        x.Id != id
                        && x.TenantIntegrationId == r.TenantIntegrationId
                        && x.ExternalDiscountTypeId == trimmed)).Any();
                    if (dup) return ServiceResult<bool>.Fail("Bu ExternalDiscountTypeId artıq mövcuddur.");
                    r.ExternalDiscountTypeId = trimmed;
                }
            }
            if (dto.DiscountName != null) r.DiscountName = dto.DiscountName.Trim();
            if (dto.IsActive.HasValue) r.IsActive = dto.IsActive.Value;

            _repo.Update(r);
            _uow.Commit();
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(int id, int? tenantId, CancellationToken ct = default)
        {
            var r = await _repo.GetByIdAsync(id, ct);
            if (r == null) return ServiceResult<bool>.Fail("Tapılmadı.");

            var ti = await _tenantIntegrationRepo.GetByIdAsync(r.TenantIntegrationId, ct);
            if (ti == null || (tenantId.HasValue && ti.TenantId != tenantId.Value))
                return ServiceResult<bool>.Fail("Yetkisiz.");

            r.IsActive = false;
            _repo.Update(r);
            _uow.Commit();
            return ServiceResult<bool>.Ok(true);
        }

        private static PosLoyaltyDiscountTypeDto Map(PosLoyaltyDiscountType e) => new()
        {
            Id = e.Id,
            TenantIntegrationId = e.TenantIntegrationId,
            ExternalDiscountTypeId = e.ExternalDiscountTypeId,
            DiscountName = e.DiscountName,
            IsActive = e.IsActive
        };
    }
}
