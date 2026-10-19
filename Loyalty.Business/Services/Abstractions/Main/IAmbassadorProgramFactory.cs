using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public sealed record CreateAmbassadorProgramResultDto(int ProgramId, int CardTemplateId, string ProgramName);

    /// <summary>Ambassador proqramı + adi kartın dizaynından kopyalanmış şablon (tenant admin düyməsi və kassa idxalı).</summary>
    public interface IAmbassadorProgramFactory
    {
        /// <summary>Yoxlamalarla (ambassador funksiyası açıq, aktiv ambassador proqramı yoxdur) — tenant admin düyməsi.</summary>
        Task<ServiceResult<CreateAmbassadorProgramResultDto>> CreateForTenantAsync(int tenantId, int userId, AmbassadorCardKind kind,
            int? totalSteps, string? name, CancellationToken ct = default);

        /// <summary>Yoxlamasız yaradılış (çağıran yoxlayır; açıq transaction-da işləyir). SaveChanges çağırır.</summary>
        Task<LoyaltyProgram> CreateAsync(int tenantId, int userId, AmbassadorCardKind kind, int? totalSteps, string? name,
            PosEarnSource? earnSource, DateTime now, CancellationToken ct = default);
    }
}
