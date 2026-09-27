using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
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
        private readonly IHappyHoursWindowRepository _happyHoursWindowRepo;
        private readonly IStorage _storage;

        public LoyaltyProgramService(IStorage storage, ILoyaltyProgramRepository repo, IMapper mapper, IUnitOfWork unitOfWork, IHappyHoursWindowRepository happyHoursWindowRepo)
        {
            _storage = storage;
            _repo = repo;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _happyHoursWindowRepo = happyHoursWindowRepo;
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

            // --- HappyHours validation (bütün program tipləri üçün — override-all) ---
            if (dto.BasePercentCash is <= 0 || dto.BasePercentCard is <= 0)
                return new ServiceResult(false, "Base percent values must be greater than 0.");
            if (dto.ProgramType == LoyaltyProgramType.HappyHoursBased
                && dto.BasePercentCash is null && dto.BasePercentCard is null
                && (dto.HappyHoursWindows is null || dto.HappyHoursWindows.Count == 0))
            {
                return new ServiceResult(false, "HappyHours program requires at least one base percent or window.");
            }

            if (dto.HappyHoursWindows != null)
            {
                foreach (var w in dto.HappyHoursWindows)
                {
                    if (w.EndTime <= w.StartTime)
                        return new ServiceResult(false, "HappyHours window EndTime must be greater than StartTime.");
                    if (w.PercentCash <= 0 || w.PercentCard <= 0)
                        return new ServiceResult(false, "HappyHours window percent values must be greater than 0.");
                    if (w.DayOfWeek is not null && (w.DayOfWeek > 6))
                        return new ServiceResult(false, "HappyHours window DayOfWeek must be 0..6.");
                }
            }

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
                existingProgram.IsActive = dto.IsActive;
                existingProgram.IsLotteryEnabled = dto.IsLotteryEnabled;
                existingProgram.LotteryPurchaseThreshold = dto.LotteryPurchaseThreshold;
                existingProgram.LotteryAmountThreshold = dto.LotteryAmountThreshold;
                existingProgram.BasePercentCash = dto.BasePercentCash;
                existingProgram.BasePercentCard = dto.BasePercentCard;

                // Update entity in the database
                _unitOfWork.Repository<LoyaltyProgram>().Update(existingProgram);

                // Sync HappyHours windows: replace strategy
                await _happyHoursWindowRepo.DeleteByProgramAsync(existingProgram.Id, ct);
                if (dto.HappyHoursWindows is { Count: > 0 })
                {
                    foreach (var w in dto.HappyHoursWindows)
                    {
                        await _unitOfWork.Repository<HappyHoursWindow>().AddAsync(new HappyHoursWindow
                        {
                            LoyaltyProgramId = existingProgram.Id,
                            DayOfWeek = w.DayOfWeek,
                            SpecificDate = w.SpecificDate,
                            StartTime = w.StartTime,
                            EndTime = w.EndTime,
                            PercentCash = w.PercentCash,
                            PercentCard = w.PercentCard,
                            Label = w.Label?.Trim()
                        });
                    }
                }

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
                    existingProgram.PointsExpiryDays,
                    existingProgram.IsLotteryEnabled,
                    existingProgram.LotteryPurchaseThreshold,
                    existingProgram.LotteryAmountThreshold,
                    existingProgram.BasePercentCash,
                    existingProgram.BasePercentCard,
                    HappyHoursWindows = dto.HappyHoursWindows ?? new List<HappyHoursWindowDto>()
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
                    PointsExpiryDays = dto.PointsExpiryDays,
                    IsActive = dto.IsActive,
                    IsLotteryEnabled = dto.IsLotteryEnabled,
                    LotteryPurchaseThreshold = dto.LotteryPurchaseThreshold,
                    LotteryAmountThreshold = dto.LotteryAmountThreshold,
                    BasePercentCash = dto.BasePercentCash,
                    BasePercentCard = dto.BasePercentCard
                };

                await _unitOfWork.Repository<LoyaltyProgram>().AddAsync(entity);
                _unitOfWork.Commit();

                // Add HappyHours windows after we have the program Id
                if (dto.HappyHoursWindows is { Count: > 0 })
                {
                    foreach (var w in dto.HappyHoursWindows)
                    {
                        await _unitOfWork.Repository<HappyHoursWindow>().AddAsync(new HappyHoursWindow
                        {
                            LoyaltyProgramId = entity.Id,
                            DayOfWeek = w.DayOfWeek,
                            SpecificDate = w.SpecificDate,
                            StartTime = w.StartTime,
                            EndTime = w.EndTime,
                            PercentCash = w.PercentCash,
                            PercentCard = w.PercentCard,
                            Label = w.Label?.Trim()
                        });
                    }
                    _unitOfWork.Commit();
                }

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
                    entity.PointsExpiryDays,
                    entity.IsLotteryEnabled,
                    entity.LotteryPurchaseThreshold,
                    entity.LotteryAmountThreshold,
                    entity.BasePercentCash,
                    entity.BasePercentCard,
                    HappyHoursWindows = dto.HappyHoursWindows ?? new List<HappyHoursWindowDto>()
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
            while (await _repo.GetByCodeAsync(tenantId, code, ct)!=null)
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
        public async Task<ServiceResult> Delete(int id)
        {
            var entity = await _unitOfWork.Repository<LoyaltyProgram>().GetByIdAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Loyalty program not found.");

            entity.IsActive = false;
            _unitOfWork.Repository<LoyaltyProgram>().Update(entity);
            _unitOfWork.Commit();

            return new ServiceResult(true, "Loyalty program deleted successfully.");
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

        public async Task<ServiceResult> GetActiveByTenantAsync(int tenantId)
        {
            var active = await _repo.GetActive();
            var scoped = (active ?? new List<LoyaltyProgram>())
                .Where(p => p.TenantId == tenantId)
                .ToList();
            var response = _mapper.Map<ICollection<GetLoyaltyProgramDto>>(scoped);
            return new ServiceResult(true, response);
        }
        public Task<ServiceResult> GetLast()
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult> Update(AddLoyaltyProgramDto dto, int id)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> UpdateHappyHoursAsync(int programId, UpdateHappyHoursDto dto, CancellationToken ct = default)
        {
            if (dto is null)
                return new ServiceResult(false, "Model is null.");

            var program = await _repo.GetByIdTrackedAsync(programId, ct);
            if (program is null)
                return new ServiceResult(false, "Loyalty program not found.");

            // 0% cashback icazə verilmir
            if (dto.BasePercentCash is <= 0 || dto.BasePercentCard is <= 0)
                return new ServiceResult(false, "Base percent values must be greater than 0.");

            if (dto.HappyHoursWindows is not null)
            {
                foreach (var w in dto.HappyHoursWindows)
                {
                    if (w.EndTime <= w.StartTime)
                        return new ServiceResult(false, "HappyHours window EndTime must be greater than StartTime.");
                    if (w.PercentCash <= 0 || w.PercentCard <= 0)
                        return new ServiceResult(false, "HappyHours window percent values must be greater than 0.");
                    if (w.DayOfWeek is not null && w.DayOfWeek > 6)
                        return new ServiceResult(false, "HappyHours window DayOfWeek must be 0..6.");
                }
            }

            program.BasePercentCash = dto.BasePercentCash;
            program.BasePercentCard = dto.BasePercentCard;
            _unitOfWork.Repository<LoyaltyProgram>().Update(program);

            await _happyHoursWindowRepo.DeleteByProgramAsync(programId, ct);
            if (dto.HappyHoursWindows is { Count: > 0 })
            {
                foreach (var w in dto.HappyHoursWindows)
                {
                    await _unitOfWork.Repository<HappyHoursWindow>().AddAsync(new HappyHoursWindow
                    {
                        LoyaltyProgramId = programId,
                        DayOfWeek = w.DayOfWeek,
                        SpecificDate = w.SpecificDate,
                        StartTime = w.StartTime,
                        EndTime = w.EndTime,
                        PercentCash = w.PercentCash,
                        PercentCard = w.PercentCard,
                        Label = w.Label?.Trim()
                    });
                }
            }

            _unitOfWork.Commit();
            return new ServiceResult(true, new { programId, dto.BasePercentCash, dto.BasePercentCard, count = dto.HappyHoursWindows?.Count ?? 0 });
        }

        public async Task<ServiceResult> UpdateWelcomeBonusAsync(int programId, UpdateWelcomeBonusDto dto, int? ownershipTenantId, CancellationToken ct = default)
        {
            if (dto is null)
                return new ServiceResult(false, "Model is null.");

            var program = await _repo.GetByIdTrackedAsync(programId, ct);
            if (program is null)
                return new ServiceResult(false, "Loyalty program not found.");
            if (ownershipTenantId.HasValue && program.TenantId != ownershipTenantId.Value)
                return new ServiceResult(false, "Bu proqram sizin tenant-a aid deyil.");

            CardTemplateType? templateType = null;
            if (program.CardTemplateId.HasValue)
            {
                var tpl = await _unitOfWork.Repository<CardTemplate>().GetByIdAsync(program.CardTemplateId.Value, ct);
                templateType = tpl?.TemplateType;
            }

            if (dto.FirstPurchaseBonusEnabled)
            {
                if (dto.FirstPurchaseBonusType == WelcomeBonusType.Percent && (dto.FirstPurchaseBonusValue <= 0 || dto.FirstPurchaseBonusValue > 100))
                    return new ServiceResult(false, "Faiz 0-dan böyük, 100-dən kiçik və ya bərabər olmalıdır.");
                if (dto.FirstPurchaseBonusType == WelcomeBonusType.Fixed && dto.FirstPurchaseBonusValue <= 0)
                    return new ServiceResult(false, "Sabit bonus 0-dan böyük olmalıdır.");
                if (dto.FirstPurchaseBonusMaxPoints is <= 0)
                    return new ServiceResult(false, "Maksimum bonus 0-dan böyük olmalıdır.");
            }

            if (dto.SignupGiftEnabled)
            {
                if (dto.SignupGiftValue < 1 || dto.SignupGiftValue > 1000)
                    return new ServiceResult(false, "Hədiyyə dəyəri 1..1000 aralığında olmalıdır.");
                if (templateType == CardTemplateType.ProgressBased && dto.SignupGiftType == SignupGiftType.Points)
                    return new ServiceResult(false, "ProgressBased kart üçün hədiyyə tipi Stamp və ya Pulsuz məhsul olmalıdır.");
                if (templateType is CardTemplateType.PointsBased or CardTemplateType.TierBased && dto.SignupGiftType != SignupGiftType.Points)
                    return new ServiceResult(false, "Xal kartı üçün hədiyyə tipi Xal olmalıdır.");
            }

            program.FirstPurchaseBonusEnabled = dto.FirstPurchaseBonusEnabled;
            program.FirstPurchaseBonusType = dto.FirstPurchaseBonusType;
            program.FirstPurchaseBonusValue = dto.FirstPurchaseBonusValue;
            program.FirstPurchaseBonusMaxPoints = dto.FirstPurchaseBonusType == WelcomeBonusType.Percent ? dto.FirstPurchaseBonusMaxPoints : null;
            program.SignupGiftEnabled = dto.SignupGiftEnabled;
            program.SignupGiftType = dto.SignupGiftType;
            program.SignupGiftValue = dto.SignupGiftValue;
            program.EditDate = DateTime.UtcNow;

            _unitOfWork.Repository<LoyaltyProgram>().Update(program);
            _unitOfWork.Commit();

            return new ServiceResult(true, _mapper.Map<GetLoyaltyProgramDto>(program));
        }
    }
}
