using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class PosProductEarnRuleService : IPosProductEarnRuleService
    {
        private readonly IPosProductEarnRuleRepository _repo;
        private readonly ITenantIntegrationRepository _tenantIntegrationRepo;
        private readonly IUnitOfWork _uow;

        public PosProductEarnRuleService(
            IPosProductEarnRuleRepository repo,
            ITenantIntegrationRepository tenantIntegrationRepo,
            IUnitOfWork uow)
        {
            _repo = repo;
            _tenantIntegrationRepo = tenantIntegrationRepo;
            _uow = uow;
        }

        public async Task<ServiceResult<PosProductEarnRuleDto>> CreateAsync(PosProductEarnRuleCreateDto dto, int? tenantId, CancellationToken ct = default)
        {
            var ti = await _tenantIntegrationRepo.GetByIdAsync(dto.TenantIntegrationId, ct);
            if (ti == null) return ServiceResult<PosProductEarnRuleDto>.Fail("TenantIntegration tapılmadı.");
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<PosProductEarnRuleDto>.Fail("Yetkisiz.");

            var entity = new PosProductEarnRule
            {
                TenantIntegrationId = dto.TenantIntegrationId,
                ExternalProductId = dto.ExternalProductId?.Trim() ?? string.Empty,
                ProductName = dto.ProductName?.Trim(),
                StepsPerUnit = dto.StepsPerUnit,
                PointsPerUnit = dto.PointsPerUnit,
                RegUserId = 1,
                RegDate = DateTime.UtcNow
            };
            await _repo.AddAsync(entity);
            _uow.Commit();

            var saved = await _repo.GetByIdAsync(entity.Id, ct);
            if (saved == null) return ServiceResult<PosProductEarnRuleDto>.Fail("Yaradıla bilmədi.");

            return ServiceResult<PosProductEarnRuleDto>.Ok(new PosProductEarnRuleDto
            {
                Id = saved.Id,
                TenantIntegrationId = saved.TenantIntegrationId,
                ExternalProductId = saved.ExternalProductId,
                ProductName = saved.ProductName,
                StepsPerUnit = saved.StepsPerUnit,
                PointsPerUnit = saved.PointsPerUnit
            });
        }

        public async Task<ServiceResult<List<PosProductEarnRuleDto>>> ListByTenantIntegrationAsync(int tenantIntegrationId, int? tenantId, CancellationToken ct = default)
        {
            var ti = await _tenantIntegrationRepo.GetByIdAsync(tenantIntegrationId, ct);
            if (ti == null) return ServiceResult<List<PosProductEarnRuleDto>>.Fail("TenantIntegration tapılmadı.");
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<List<PosProductEarnRuleDto>>.Fail("Yetkisiz.");

            var list = await _repo.GetByTenantIntegrationIdAsync(tenantIntegrationId, ct);
            var dtos = list.Select(r => new PosProductEarnRuleDto
            {
                Id = r.Id,
                TenantIntegrationId = r.TenantIntegrationId,
                ExternalProductId = r.ExternalProductId,
                ProductName = r.ProductName,
                StepsPerUnit = r.StepsPerUnit,
                PointsPerUnit = r.PointsPerUnit
            }).ToList();
            return ServiceResult<List<PosProductEarnRuleDto>>.Ok(dtos);
        }

        public async Task<ServiceResult<bool>> UpdateAsync(int id, PosProductEarnRuleUpdateDto dto, int? tenantId, CancellationToken ct = default)
        {
            var r = await _repo.GetByIdAsync(id, ct);
            if (r == null) return ServiceResult<bool>.Fail("Tapılmadı.");

            var ti = await _tenantIntegrationRepo.GetByIdAsync(r.TenantIntegrationId, ct);
            if (ti == null || (tenantId.HasValue && ti.TenantId != tenantId.Value))
                return ServiceResult<bool>.Fail("Yetkisiz.");

            if (dto.ExternalProductId != null) r.ExternalProductId = dto.ExternalProductId.Trim();
            if (dto.ProductName != null) r.ProductName = dto.ProductName.Trim();
            if (dto.StepsPerUnit.HasValue) r.StepsPerUnit = dto.StepsPerUnit.Value;
            if (dto.PointsPerUnit.HasValue) r.PointsPerUnit = dto.PointsPerUnit.Value;

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

            _repo.Delete(r);
            _uow.Commit();
            return ServiceResult<bool>.Ok(true);
        }
    }
}
