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
        private readonly ILoyaltyProgramRepository _programRepo;
        private readonly IUnitOfWork _uow;

        public PosProductEarnRuleService(
            IPosProductEarnRuleRepository repo,
            ITenantIntegrationRepository tenantIntegrationRepo,
            ILoyaltyProgramRepository programRepo,
            IUnitOfWork uow)
        {
            _programRepo = programRepo;
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

            if (dto.LoyaltyProgramId is > 0 && !await ProgramBelongsToTenantAsync(dto.LoyaltyProgramId.Value, ti.TenantId, ct))
                return ServiceResult<PosProductEarnRuleDto>.Fail("Proqram bu tenant-a aid deyil.");

            if (!Enum.IsDefined(dto.Kind)) return ServiceResult<PosProductEarnRuleDto>.Fail("Qayda növü yanlışdır.");
            if (string.IsNullOrWhiteSpace(dto.ExternalProductId)) return ServiceResult<PosProductEarnRuleDto>.Fail("ID boşdur.");

            var entity = new PosProductEarnRule
            {
                TenantIntegrationId = dto.TenantIntegrationId,
                LoyaltyProgramId = dto.LoyaltyProgramId is > 0 ? dto.LoyaltyProgramId : null,
                Kind = dto.Kind,
                ExternalProductId = dto.ExternalProductId.Trim(),
                Code = dto.Kind == PosEarnExclusionKind.Product ? NullIfEmpty(dto.Code) : null,
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

            return ServiceResult<PosProductEarnRuleDto>.Ok(ToDto(saved));
        }

        public async Task<ServiceResult<List<PosProductEarnRuleDto>>> ListByTenantIntegrationAsync(int tenantIntegrationId, int? tenantId, CancellationToken ct = default)
        {
            var ti = await _tenantIntegrationRepo.GetByIdAsync(tenantIntegrationId, ct);
            if (ti == null) return ServiceResult<List<PosProductEarnRuleDto>>.Fail("TenantIntegration tapılmadı.");
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<List<PosProductEarnRuleDto>>.Fail("Yetkisiz.");

            var list = await _repo.GetByTenantIntegrationIdAsync(tenantIntegrationId, ct);
            var dtos = list.Select(ToDto).ToList();
            return ServiceResult<List<PosProductEarnRuleDto>>.Ok(dtos);
        }

        public async Task<ServiceResult<bool>> UpdateAsync(int id, PosProductEarnRuleUpdateDto dto, int? tenantId, CancellationToken ct = default)
        {
            var r = await _repo.GetByIdAsync(id, ct);
            if (r == null) return ServiceResult<bool>.Fail("Tapılmadı.");

            var ti = await _tenantIntegrationRepo.GetByIdAsync(r.TenantIntegrationId, ct);
            if (ti == null || (tenantId.HasValue && ti.TenantId != tenantId.Value))
                return ServiceResult<bool>.Fail("Yetkisiz.");

            if (!string.IsNullOrWhiteSpace(dto.ExternalProductId)) r.ExternalProductId = dto.ExternalProductId.Trim();
            if (dto.Code != null && r.Kind == PosEarnExclusionKind.Product) r.Code = NullIfEmpty(dto.Code);
            if (dto.ProductName != null) r.ProductName = dto.ProductName.Trim();
            if (dto.StepsPerUnit.HasValue) r.StepsPerUnit = dto.StepsPerUnit.Value;
            if (dto.PointsPerUnit.HasValue) r.PointsPerUnit = dto.PointsPerUnit.Value;
            if (dto.LoyaltyProgramId.HasValue)
            {
                if (dto.LoyaltyProgramId.Value > 0 && !await ProgramBelongsToTenantAsync(dto.LoyaltyProgramId.Value, ti.TenantId, ct))
                    return ServiceResult<bool>.Fail("Proqram bu tenant-a aid deyil.");
                r.LoyaltyProgramId = dto.LoyaltyProgramId.Value > 0 ? dto.LoyaltyProgramId.Value : null;
            }

            _repo.Update(r);
            _uow.Commit();
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<PosProductEarnRuleBulkResultDto>> BulkUpsertAsync(
            int tenantIntegrationId, List<PosProductEarnRuleCreateDto> items, int? tenantId, CancellationToken ct = default)
        {
            if (items == null || items.Count == 0)
                return ServiceResult<PosProductEarnRuleBulkResultDto>.Fail("Siyahı boşdur.");
            if (items.Count > 500)
                return ServiceResult<PosProductEarnRuleBulkResultDto>.Fail("Bir dəfəyə ən çox 500 qayda.");

            var ti = await _tenantIntegrationRepo.GetByIdAsync(tenantIntegrationId, ct);
            if (ti == null) return ServiceResult<PosProductEarnRuleBulkResultDto>.Fail("TenantIntegration tapılmadı.");
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<PosProductEarnRuleBulkResultDto>.Fail("Yetkisiz.");

            foreach (var programId in items.Where(i => i.LoyaltyProgramId is > 0).Select(i => i.LoyaltyProgramId!.Value).Distinct())
                if (!await ProgramBelongsToTenantAsync(programId, ti.TenantId, ct))
                    return ServiceResult<PosProductEarnRuleBulkResultDto>.Fail("Proqram bu tenant-a aid deyil.");

            var existing = await _repo.GetByTenantIntegrationIdAsync(tenantIntegrationId, ct);
            static string KeyOf(int? programId, PosEarnExclusionKind kind, string id)
                => $"{(programId is > 0 ? programId : 0)}|{(int)kind}|{id.Trim().ToLowerInvariant()}";
            var byKey = existing
                .GroupBy(r => KeyOf(r.LoyaltyProgramId, r.Kind, r.ExternalProductId))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First());

            var result = new PosProductEarnRuleBulkResultDto();
            var now = DateTime.UtcNow;
            foreach (var dto in items)
            {
                if (!Enum.IsDefined(dto.Kind) || string.IsNullOrWhiteSpace(dto.ExternalProductId)) { result.Skipped++; continue; }
                var programId = dto.LoyaltyProgramId is > 0 ? dto.LoyaltyProgramId : null;
                var code = dto.Kind == PosEarnExclusionKind.Product ? NullIfEmpty(dto.Code) : null;
                var key = KeyOf(programId, dto.Kind, dto.ExternalProductId);

                if (byKey.TryGetValue(key, out var r))
                {
                    r.StepsPerUnit = dto.StepsPerUnit;
                    r.PointsPerUnit = dto.PointsPerUnit;
                    if (!string.IsNullOrWhiteSpace(dto.ProductName)) r.ProductName = dto.ProductName.Trim();
                    if (code != null) r.Code = code;
                    if (r.Id > 0) _repo.Update(r);
                    result.Updated++;
                    continue;
                }

                var entity = new PosProductEarnRule
                {
                    TenantIntegrationId = tenantIntegrationId,
                    LoyaltyProgramId = programId,
                    Kind = dto.Kind,
                    ExternalProductId = dto.ExternalProductId.Trim(),
                    Code = code,
                    ProductName = dto.ProductName?.Trim(),
                    StepsPerUnit = dto.StepsPerUnit,
                    PointsPerUnit = dto.PointsPerUnit,
                    RegUserId = 1,
                    RegDate = now
                };
                await _repo.AddAsync(entity);
                byKey[key] = entity;
                result.Created++;
            }

            _uow.Commit();
            return ServiceResult<PosProductEarnRuleBulkResultDto>.Ok(result);
        }

        private static PosProductEarnRuleDto ToDto(PosProductEarnRule r) => new()
        {
            Id = r.Id,
            TenantIntegrationId = r.TenantIntegrationId,
            Kind = r.Kind,
            ExternalProductId = r.ExternalProductId,
            Code = r.Code,
            ProductName = r.ProductName,
            StepsPerUnit = r.StepsPerUnit,
            PointsPerUnit = r.PointsPerUnit,
            LoyaltyProgramId = r.LoyaltyProgramId
        };

        private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        private async Task<bool> ProgramBelongsToTenantAsync(int programId, int tenantId, CancellationToken ct)
        {
            var program = await _programRepo.GetByIdAsync(programId, ct);
            return program != null && program.TenantId == tenantId;
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
