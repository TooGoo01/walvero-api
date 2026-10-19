using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Ambassador proqramı + şablon. Dizayn tenant-ın adi (Public) kartından kopyalanır (rənglər, şəkillər, DesignJson),
    /// sonra "Kart dizaynı"nda ayrıca dəyişdirilir.
    /// </summary>
    public sealed class AmbassadorProgramFactory : IAmbassadorProgramFactory
    {
        private readonly LoyaltyWalletDbContext _db;
        private readonly ILogger<AmbassadorProgramFactory> _logger;

        public AmbassadorProgramFactory(LoyaltyWalletDbContext db, ILogger<AmbassadorProgramFactory> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<ServiceResult<CreateAmbassadorProgramResultDto>> CreateForTenantAsync(int tenantId, int userId, AmbassadorCardKind kind,
            int? totalSteps, string? name, CancellationToken ct = default)
        {
            if (!Enum.IsDefined(kind))
                return ServiceResult<CreateAmbassadorProgramResultDto>.Fail("Balans növü düzgün deyil.");

            var tenant = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => new { t.EnableAmbassadors })
                .FirstOrDefaultAsync(ct);
            if (tenant == null)
                return ServiceResult<CreateAmbassadorProgramResultDto>.Fail("Tenant tapılmadı.");
            if (!tenant.EnableAmbassadors)
                return ServiceResult<CreateAmbassadorProgramResultDto>.Fail("Ambassador funksiyası söndürülüb — super admin açmalıdır.");

            var exists = await _db.LoyaltyPrograms.AnyAsync(
                p => p.TenantId == tenantId && p.IsActive && p.Audience == ProgramAudience.Ambassador, ct);
            if (exists)
                return ServiceResult<CreateAmbassadorProgramResultDto>.Fail("Ambassador kartı artıq var — «Kart dizaynı»nda proqram seçimindən açın.");

            try
            {
                var program = await CreateAsync(tenantId, userId, kind, totalSteps, name, null, DateTime.UtcNow, ct);
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Ambassador program created. TenantId={TenantId}, ProgramId={ProgramId}, Kind={Kind}", tenantId, program.Id, kind);
                return ServiceResult<CreateAmbassadorProgramResultDto>.Ok(
                    new CreateAmbassadorProgramResultDto(program.Id, program.CardTemplateId ?? 0, program.ProgramName));
            }
            catch (DbUpdateException ex)
            {
                // Paralel klik: filtered unique index (tenant başına bir aktiv ambassador proqramı)
                _logger.LogWarning(ex, "Ambassador program create failed. TenantId={TenantId}", tenantId);
                return ServiceResult<CreateAmbassadorProgramResultDto>.Fail("Ambassador kartı yaradılmadı — artıq mövcud ola bilər, səhifəni yeniləyin.");
            }
        }

        public async Task<LoyaltyProgram> CreateAsync(int tenantId, int userId, AmbassadorCardKind kind, int? totalSteps, string? name,
            PosEarnSource? earnSource, DateTime now, CancellationToken ct = default)
        {
            var source = await _db.LoyaltyPrograms.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.IsActive && p.Audience != ProgramAudience.Ambassador && p.CardTemplate != null)
                .OrderBy(p => p.Id)
                .Select(p => p.CardTemplate!)
                .FirstOrDefaultAsync(ct);
            var spec = AmbassadorCardSpec.For(kind, totalSteps, source?.Currency, earnSource);

            var code = "AMBASSADOR";
            for (var i = 1; await _db.LoyaltyPrograms.AnyAsync(p => p.TenantId == tenantId && p.Code == code, ct); i++)
                code = $"AMBASSADOR-{i}";

            var programName = string.IsNullOrWhiteSpace(name) ? "Ambassador" : name.Trim();
            if (programName.Length > 100) programName = programName[..100];

            var program = new LoyaltyProgram
            {
                TenantId = tenantId,
                ProgramName = programName,
                Code = code,
                ProgramType = spec.ProgramType,
                Audience = ProgramAudience.Ambassador,
                PosEarnSource = spec.EarnSource,
                PointsPerCurrency = 1,
                CurrencyPerPoint = 1,
                IsActive = true,
                RegUserId = userId,
                RegDate = now
            };
            _db.LoyaltyPrograms.Add(program);
            await _db.SaveChangesAsync(ct);

            var stampsSource = source?.TemplateType == CardTemplateType.ProgressBased ? source : null;
            var template = new CardTemplate
            {
                LoyaltyProgramId = program.Id,
                TemplateName = programName,
                TemplateType = spec.TemplateType,
                Currency = spec.Currency ?? source?.Currency ?? "AZN",
                TotalSteps = spec.TotalSteps ?? source?.TotalSteps ?? AmbassadorCardSpec.DefaultTotalSteps,
                StepLabel = stampsSource?.StepLabel ?? "Coffee",
                RewardDescription = stampsSource?.RewardDescription ?? string.Empty,
                FreeRewardLabel = stampsSource?.FreeRewardLabel,
                BackgroundColor = source?.BackgroundColor ?? "#FFFFFF",
                ForegroundColor = source?.ForegroundColor ?? "#000000",
                LabelColor = source?.LabelColor ?? "#999999",
                ProgressBarColor = source?.ProgressBarColor ?? "#4CAF50",
                LogoUrl = source?.LogoUrl,
                GoogleLogoUrl = source?.GoogleLogoUrl,
                AppLogoUrl = source?.AppLogoUrl,
                BackgroundImageUrl = source?.BackgroundImageUrl,
                StripImageUrl = source?.StripImageUrl,
                HeroImageUrl = source?.HeroImageUrl,
                AppStripUrl = source?.AppStripUrl,
                DesignJson = source?.DesignJson,
                IconLayout = source?.IconLayout ?? IconLayout.Grid,
                RegUserId = userId,
                RegDate = now
            };
            _db.CardTemplates.Add(template);
            await _db.SaveChangesAsync(ct);

            program.CardTemplateId = template.Id;
            return program;
        }
    }
}
