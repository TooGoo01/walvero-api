using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Utilities.Security.Encryption;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class TenantIntegrationService : ITenantIntegrationService
    {
        private readonly ITenantIntegrationRepository _repo;
        private readonly IIntegrationTypeRepository _typeRepo;
        private readonly IWalletTokenGenerator _tokenGenerator;
        private readonly ISensitiveDataProtector _protector;
        private readonly IUnitOfWork _uow;

        public TenantIntegrationService(
            ITenantIntegrationRepository repo,
            IIntegrationTypeRepository typeRepo,
            IWalletTokenGenerator tokenGenerator,
            ISensitiveDataProtector protector,
            IUnitOfWork uow)
        {
            _repo = repo;
            _typeRepo = typeRepo;
            _tokenGenerator = tokenGenerator;
            _protector = protector;
            _uow = uow;
        }

        public async Task<ServiceResult<TenantIntegrationCreateResult>> CreateAsync(TenantIntegrationCreateDto dto, CancellationToken ct = default)
        {
            var existing = await _repo.GetActiveByTenantAndTypeAsync(dto.TenantId, dto.IntegrationTypeId, ct);
            if (existing != null)
                return ServiceResult<TenantIntegrationCreateResult>.Fail("Bu tenant üçün bu inteqrasiya artıq mövcuddur.");

            var (token, hash) = _tokenGenerator.Generate();
            var entity = new TenantIntegration
            {
                TenantId = dto.TenantId,
                IntegrationTypeId = dto.IntegrationTypeId,
                DisplayName = dto.DisplayName,
                ExternalVenueId = dto.ExternalVenueId,
                LoyaltyProgramId = dto.LoyaltyProgramId,
                ApiBaseUrl = dto.ApiBaseUrl?.Trim(),
                ApiUsername = dto.ApiUsername?.Trim(),
                ApiTokenEncrypted = !string.IsNullOrEmpty(dto.ApiToken) ? _protector.Encrypt(dto.ApiToken) : null,
                WalletTokenHash = hash,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                RegUserId = 1,
                RegDate = DateTime.UtcNow
            };
            await _repo.AddAsync(entity);
            await _uow.CommitAsync(ct);

            var saved = await _repo.GetActiveByTenantAndTypeAsync(dto.TenantId, dto.IntegrationTypeId, ct);
            if (saved == null) return ServiceResult<TenantIntegrationCreateResult>.Fail("Yaradıla bilmədi.");

            return ServiceResult<TenantIntegrationCreateResult>.Ok(new TenantIntegrationCreateResult
            {
                Id = saved.Id,
                WalletToken = token,
                WalletWebhookBaseUrl = "api/wallet"
            });
        }

        public async Task<ServiceResult<List<TenantIntegrationListDto>>> ListByTenantAsync(int tenantId, CancellationToken ct = default)
        {
            var list = await _repo.GetAllAsync(ti => ti.TenantId == tenantId);
            var dtos = new List<TenantIntegrationListDto>();
            foreach (var ti in list)
            {
                var it = await _typeRepo.GetByIdAsync(ti.IntegrationTypeId, ct);
                dtos.Add(new TenantIntegrationListDto
                {
                    Id = ti.Id,
                    TenantId = ti.TenantId,
                    IntegrationTypeName = it?.Name ?? "",
                    Source = it?.Source ?? "",
                    DisplayName = ti.DisplayName,
                    ExternalVenueId = ti.ExternalVenueId,
                    IsActive = ti.IsActive
                });
            }
            return ServiceResult<List<TenantIntegrationListDto>>.Ok(dtos);
        }

        public async Task<ServiceResult<TenantIntegrationDetailDto?>> GetByIdAsync(int id, int? tenantId, CancellationToken ct = default)
        {
            var ti = await _repo.GetByIdAsync(id, ct);
            if (ti == null) return ServiceResult<TenantIntegrationDetailDto?>.Ok(null);
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<TenantIntegrationDetailDto?>.Fail("Yetkisiz.");
            var it = await _typeRepo.GetByIdAsync(ti.IntegrationTypeId, ct);
            return ServiceResult<TenantIntegrationDetailDto?>.Ok(new TenantIntegrationDetailDto
            {
                Id = ti.Id,
                TenantId = ti.TenantId,
                IntegrationTypeId = ti.IntegrationTypeId,
                IntegrationTypeName = it?.Name ?? "",
                Source = it?.Source ?? "",
                DisplayName = ti.DisplayName,
                ExternalVenueId = ti.ExternalVenueId,
                LoyaltyProgramId = ti.LoyaltyProgramId,
                ApiBaseUrl = ti.ApiBaseUrl,
                ApiUsername = ti.ApiUsername,
                IsActive = ti.IsActive
            });
        }

        public async Task<ServiceResult<bool>> UpdateAsync(int id, TenantIntegrationUpdateDto dto, int? tenantId, CancellationToken ct = default)
        {
            var ti = await _repo.GetByIdAsync(id, ct);
            if (ti == null) return ServiceResult<bool>.Fail("Tapılmadı.");
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<bool>.Fail("Yetkisiz.");

            if (dto.DisplayName != null) ti.DisplayName = dto.DisplayName;
            if (dto.ExternalVenueId != null) ti.ExternalVenueId = dto.ExternalVenueId;
            if (dto.LoyaltyProgramId.HasValue) ti.LoyaltyProgramId = dto.LoyaltyProgramId;
            if (dto.ApiBaseUrl != null) ti.ApiBaseUrl = dto.ApiBaseUrl.Trim();
            if (dto.ApiUsername != null) ti.ApiUsername = dto.ApiUsername.Trim();
            if (!string.IsNullOrEmpty(dto.ApiToken)) ti.ApiTokenEncrypted = _protector.Encrypt(dto.ApiToken);
            if (dto.IsActive.HasValue) ti.IsActive = dto.IsActive.Value;
            ti.UpdatedAt = DateTime.UtcNow;
            _repo.Update(ti);
            await _uow.CommitAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(int id, int? tenantId, CancellationToken ct = default)
        {
            var ti = await _repo.GetByIdAsync(id, ct);
            if (ti == null) return ServiceResult<bool>.Fail("Tapılmadı.");
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<bool>.Fail("Yetkisiz.");
            ti.IsActive = false;
            ti.UpdatedAt = DateTime.UtcNow;
            _repo.Update(ti);
            await _uow.CommitAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<string?>> RegenerateWalletTokenAsync(int id, int? tenantId, CancellationToken ct = default)
        {
            var ti = await _repo.GetByIdAsync(id, ct);
            if (ti == null) return ServiceResult<string?>.Fail("Tapılmadı.");
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<string?>.Fail("Yetkisiz.");
            var (token, hash) = _tokenGenerator.Generate();
            ti.WalletTokenHash = hash;
            ti.UpdatedAt = DateTime.UtcNow;
            _repo.Update(ti);
            await _uow.CommitAsync(ct);
            return ServiceResult<string?>.Ok(token);
        }
    }
}
