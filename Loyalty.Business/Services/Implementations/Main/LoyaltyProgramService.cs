using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class LoyaltyProgramService : ILoyaltyProgramService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILoyaltyProgramRepository _repo;
        private readonly IStorage _storage;

        public LoyaltyProgramService(IStorage storage, ILoyaltyProgramRepository repo, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _storage = storage;
            _repo = repo;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public Task<ServiceResult> Add(AddLoyaltyProgramDto Dto)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> AddOrUpdateAsync(AddLoyaltyProgramDto dto, CancellationToken ct = default)
        {
            if (dto is null)
                return new ServiceResult(false, "Model is null.");

            var tenantId = dto.TenantId;
            if (tenantId is null || tenantId <= 0)
                return new ServiceResult(false, "Tenant is not resolved.");

            // --- Validation ---
            if (string.IsNullOrWhiteSpace(dto.ProgramName))
                return new ServiceResult(false, "ProgramName is required.");

            if (dto.PointsPerCurrency <= 0)
                return new ServiceResult(false, "PointsPerCurrency must be > 0.");

            if (dto.CurrencyPerPoint <= 0)
                return new ServiceResult(false, "CurrencyPerPoint must be > 0.");

            if (dto.MinPointsToRedeem < 0)
                return new ServiceResult(false, "MinPointsToRedeem cannot be negative.");

            if (dto.PointsExpiryDays is < 0)
                return new ServiceResult(false, "PointsExpiryDays cannot be negative.");

            // --- Code normalize & ensure unique per-tenant ---
            var code = NormalizeCode(dto.Code, dto.ProgramName);
            code = await EnsureUniqueCodeAsync(tenantId.Value, code, ct);

            // --- Check if the program already exists ---
            var existingProgram = await _repo.GetByCodeAsync(dto.TenantId.Value, dto.Code, ct);

            if (existingProgram != null)
            {
                // --- Update existing program ---
                existingProgram.ProgramName = dto.ProgramName.Trim();
                existingProgram.Description = dto.Description?.Trim();
                existingProgram.PointsPerCurrency = dto.PointsPerCurrency;
                existingProgram.CurrencyPerPoint = dto.CurrencyPerPoint;
                existingProgram.ProgramType = dto.ProgramType;
                existingProgram.MinPointsToRedeem = dto.MinPointsToRedeem;
                existingProgram.PointsExpiryDays = dto.PointsExpiryDays;

                // Update entity in the database
                _unitOfWork.Repository<LoyaltyProgram>().Update(existingProgram);

                // Commit changes
                 _unitOfWork.Commit();

                // Prepare result response for updated program
                var result = new
                {
                    existingProgram.Id,
                    existingProgram.TenantId,
                    existingProgram.ProgramName,
                    existingProgram.Description,
                    existingProgram.Code,
                    existingProgram.PointsPerCurrency,
                    existingProgram.CurrencyPerPoint,
                    existingProgram.ProgramType,
                    existingProgram.MinPointsToRedeem,
                    existingProgram.PointsExpiryDays
                };

                return new ServiceResult(true, result);
            }
            else
            {
                // --- Add new program ---
                var entity = new LoyaltyProgram
                {
                    TenantId = tenantId.Value,
                    ProgramName = dto.ProgramName.Trim(),
                    Description = dto.Description?.Trim(),
                    Code = code,
                    PointsPerCurrency = dto.PointsPerCurrency,
                    CurrencyPerPoint = dto.CurrencyPerPoint,
                    ProgramType = dto.ProgramType,
                    MinPointsToRedeem = dto.MinPointsToRedeem,
                    PointsExpiryDays = dto.PointsExpiryDays
                };

                await _unitOfWork.Repository<LoyaltyProgram>().AddAsync(entity);

                // Commit changes
                 _unitOfWork.Commit();

                // Prepare result response for newly added program
                var result = new
                {
                    entity.Id,
                    entity.TenantId,
                    entity.ProgramName,
                    entity.Description,
                    entity.Code,
                    entity.PointsPerCurrency,
                    entity.CurrencyPerPoint,
                    entity.ProgramType,
                    entity.MinPointsToRedeem,
                    entity.PointsExpiryDays
                };

                return new ServiceResult(true, result);
            }
        }

        private static string NormalizeCode(string? code, string fallbackFromName)
        {
            string src = string.IsNullOrWhiteSpace(code) ? fallbackFromName : code;
            src = src.Trim().ToUpperInvariant();

            // Allow [A-Z0-9_-], replace others with '-'
            var sb = new StringBuilder(src.Length);
            foreach (var ch in src)
            {
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_' || ch == '-')
                    sb.Append(ch);
                else
                    sb.Append('-');
            }

            // compress multiple '-' and trim
            var normalized = Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
            return string.IsNullOrWhiteSpace(normalized) ? $"LP-{Guid.NewGuid():N}".Substring(0, 10) : normalized;
        }

        private async Task<string> EnsureUniqueCodeAsync(int tenantId, string baseCode, CancellationToken ct)
        {
            var code = baseCode;
            int i = 0;
            while (await _repo.GetByCodeAsync(tenantId, code, ct)==null)
            {
                i++;
                code = $"{baseCode}-{i}";
                if (i > 100) // safety
                {
                    code = $"{baseCode}-{Guid.NewGuid():N}".Substring(0, Math.Min(baseCode.Length + 5, 30));
                    break;
                }
            }
            return code;
        }
        public Task<ServiceResult> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> Get(int id)
        {
            var partners = await _repo.GetByIdNameAsync(id);
            if (partners != null)
            {
                var response = _mapper.Map<GetLoyaltyProgramDto>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetAll()
        {
            var partners = await _unitOfWork.Repository<LoyaltyProgram>().GetAllAsync();
            if (partners != null)
            {
                var response = _mapper.Map<ICollection<GetLoyaltyProgramDto>>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }
        public async Task<ServiceResult> GetActive()
        {
            var partners = await _repo.GetActive();
            if (partners != null)
            {
                var response = _mapper.Map<ICollection<GetLoyaltyProgramDto>>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }
        public Task<ServiceResult> GetLast()
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult> Update(AddLoyaltyProgramDto dto, int id)
        {
            throw new NotImplementedException();
        }
    }
}
