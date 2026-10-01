using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class CardDesignPreviewService : ICardDesignPreviewService
    {
        // Nümunə müştəri — admin önizləməsindəki (SAMPLE_CUSTOMER) ilə eyni
        private const string SampleName = "Ad Soyad";
        private const string SampleBarcode = "789000000001";
        private const decimal SamplePoints = 12.5m;
        private const int SampleSteps = 3;
        private const int SampleCycles = 1;
        // Faza 2 sahələri (ümumi xal / bitmə tarixi) — admin önizləməsi ilə eyni nümunə
        private const decimal SampleLifetime = 240m;
        private const string SampleExpiry = "31.12.2026 23:59";

        private readonly ILoyaltyProgramRepository _programRepo;
        private readonly ITenantBrandingRepository _brandingRepo;
        private readonly LoyaltyWalletDbContext _db;
        private readonly IWalletCredentialProvider _credProvider;
        private readonly IAppleWalletService _apple;
        private readonly IGoogleWalletServiceFactory _googleFactory;
        private readonly ILogger<CardDesignPreviewService> _logger;

        public CardDesignPreviewService(
            ILoyaltyProgramRepository programRepo,
            ITenantBrandingRepository brandingRepo,
            LoyaltyWalletDbContext db,
            IWalletCredentialProvider credProvider,
            IAppleWalletService apple,
            IGoogleWalletServiceFactory googleFactory,
            ILogger<CardDesignPreviewService> logger)
        {
            _programRepo = programRepo;
            _brandingRepo = brandingRepo;
            _db = db;
            _credProvider = credProvider;
            _apple = apple;
            _googleFactory = googleFactory;
            _logger = logger;
        }

        public async Task<ServiceResult> ApplePreviewAsync(int tenantId, int loyaltyProgramId, CancellationToken ct = default)
        {
            var (snapshot, error) = await BuildSnapshotAsync(tenantId, loyaltyProgramId, ct);
            if (snapshot == null) return new ServiceResult(false, error!);

            AppleWalletRuntimeCreds creds;
            try
            {
                creds = await _credProvider.GetAppleAsync(tenantId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Apple preview: creds yoxdur. TenantId={TenantId}", tenantId);
                return new ServiceResult(false, "Apple Wallet credential-ları tapılmadı.");
            }

            return await _apple.GeneratePreviewPassAsync(tenantId, loyaltyProgramId, creds, snapshot,
                SampleName, SampleBarcode, SamplePoints, SampleSteps, SampleCycles, ct, SampleLifetime, SampleExpiry);
        }

        public async Task<ServiceResult> GooglePreviewAsync(int tenantId, int loyaltyProgramId, string previewKey, CancellationToken ct = default)
        {
            var (snapshot, error) = await BuildSnapshotAsync(tenantId, loyaltyProgramId, ct);
            if (snapshot == null) return new ServiceResult(false, error!);

            var key = new string((previewKey ?? "admin").Where(char.IsLetterOrDigit).ToArray());
            if (key.Length == 0) key = "admin";
            try
            {
                var gw = await _googleFactory.CreateForTenantAsync(tenantId, loyaltyProgramId, ct);
                return await gw.PreviewAsync(snapshot, key, SampleName, SampleBarcode, SamplePoints, SampleSteps, SampleCycles, ct, SampleLifetime, SampleExpiry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google preview failed. TenantId={TenantId}, ProgramId={ProgramId}", tenantId, loyaltyProgramId);
                return new ServiceResult(false, $"Google Wallet preview alınmadı: {ex.Message}");
            }
        }

        public async Task<ServiceResult> GoogleClassStatusAsync(int tenantId, int loyaltyProgramId, CancellationToken ct = default)
        {
            var program = await _programRepo.GetByIdNameAsync(loyaltyProgramId, ct);
            if (program == null || program.TenantId != tenantId) return new ServiceResult(false, "Proqram tapılmadı.");
            try
            {
                var gw = await _googleFactory.CreateForTenantAsync(tenantId, loyaltyProgramId, ct);
                return await gw.GetClassStatusAsync(ct);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Google Wallet class statusu alınmadı: {ex.Message}");
            }
        }

        /// <summary>Saxlanmış template + branding + tenant adından snapshot (refresher ilə eyni mənbə; Icons daxil).</summary>
        private async Task<(CardTemplateSnapshot? Snapshot, string? Error)> BuildSnapshotAsync(int tenantId, int loyaltyProgramId, CancellationToken ct)
        {
            var program = await _programRepo.GetByIdNameAsync(loyaltyProgramId, ct);
            if (program == null || program.TenantId != tenantId) return (null, "Proqram tapılmadı.");

            var template = await _db.Set<CardTemplate>().AsNoTracking()
                .Include(t => t.Icons)
                .FirstOrDefaultAsync(t => t.LoyaltyProgramId == loyaltyProgramId, ct);
            if (template == null) return (null, "Kart template-i yoxdur — əvvəl dizaynı saxlayın.");

            var branding = await _brandingRepo.GetByTenantIdAsync(tenantId, ct);
            var companyName = await _db.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => t.CompanyName).FirstOrDefaultAsync(ct);

            static string ToAbs(string? p) => string.IsNullOrWhiteSpace(p) ? string.Empty
                : (p.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? p : "https://appapi.walvero.com/" + p.TrimStart('/', '\\'));

            var snapshot = CardTemplateSnapshotFactory.From(program, template, branding, ToAbs);
            snapshot.TenantCompanyName = companyName;
            snapshot.IsLotteryEnabled = false;
            return (snapshot, null);
        }
    }
}
