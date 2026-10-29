using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Kassa endirim səviyyəsi (Carpax "ENDİRİM 20%" / "Endirim 30%"): endirimi iiko kampaniyası verir, Walvero müştərini
    /// kampaniyanın qonaq kateqoriyasına yazır. Seçilə bilən kateqoriyalar kataloqun DiscountTiers siyahısından gəlir.
    /// </summary>
    public class CustomerGuestCategoryService : ICustomerGuestCategoryService
    {
        private readonly LoyaltyWalletDbContext _db;
        private readonly ITenantIntegrationRepository _integrationRepo;
        private readonly IIikoCatalogService _catalog;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CustomerGuestCategoryService> _logger;

        public CustomerGuestCategoryService(
            LoyaltyWalletDbContext db,
            ITenantIntegrationRepository integrationRepo,
            IIikoCatalogService catalog,
            IServiceScopeFactory scopeFactory,
            ILogger<CustomerGuestCategoryService> logger)
        {
            _db = db;
            _integrationRepo = integrationRepo;
            _catalog = catalog;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<ServiceResult> SetAsync(int tenantId, SetCustomerPosCategoryDto dto, CancellationToken ct = default)
        {
            var customer = await _db.Set<Customer>()
                .FirstOrDefaultAsync(c => c.Id == dto.CustomerId && c.TenantId == tenantId, ct);
            if (customer == null)
                return new ServiceResult(false, "Müştəri tapılmadı.");

            var raw = dto.CategoryId?.Trim();
            Guid? newId = null;
            string? newName = null;
            decimal? newPercent = null;
            if (!string.IsNullOrEmpty(raw))
            {
                if (!Guid.TryParse(raw, out var parsed))
                    return new ServiceResult(false, "Qonaq kateqoriyası GUID olmalıdır.");

                // Yalnız endirim kampaniyasına bağlı kateqoriya seçilə bilər (kataloq 5 dəq cache)
                var (tier, error) = await FindTierAsync(tenantId, parsed, ct);
                if (tier == null)
                    return new ServiceResult(false, error ?? "Bu kateqoriya kassa endirim kampaniyasına bağlı deyil.");
                newId = parsed;
                newName = tier.CampaignName;
                newPercent = tier.Percent > 0 ? tier.Percent : null;
            }

            Guid? oldId = Guid.TryParse(customer.PosGuestCategoryId, out var o) ? o : null;
            customer.PosGuestCategoryId = newId?.ToString();
            customer.PosGuestCategoryName = newName;
            customer.PosDiscountPercent = newPercent;
            await _db.SaveChangesAsync(ct);

            var sync = await RunCategorySyncSafeAsync(tenantId, customer.Id, oldId, newId, ct);
            EnqueuePassRefresh(tenantId, customer.Id);
            return new ServiceResult(true, sync, DescribeSync(sync, newId.HasValue));
        }

        private async Task<(IikoDiscountTierDto? Tier, string? Error)> FindTierAsync(int tenantId, Guid categoryId, CancellationToken ct)
        {
            var integrations = await _integrationRepo.GetAllActiveByTenantAsync(tenantId, ct);
            var iiko = (integrations ?? Enumerable.Empty<TenantIntegration>())
                .Where(i => i.IntegrationType?.Source == "iiko_pos")
                .ToList();
            if (iiko.Count == 0)
                return (null, "Tenant-da aktiv kassa (iiko) inteqrasiyası yoxdur.");

            var errors = new List<string>();
            foreach (var integration in iiko)
            {
                var catalog = await _catalog.GetCatalogAsync(tenantId, integration.Id, refresh: false, ct);
                if (!catalog.Success || catalog.Data == null)
                {
                    errors.Add(catalog.Message ?? "kassa kataloqu alınmadı");
                    continue;
                }
                var tier = catalog.Data.DiscountTiers
                    .FirstOrDefault(t => string.Equals(t.GuestCategoryId, categoryId.ToString(), StringComparison.OrdinalIgnoreCase));
                if (tier != null) return (tier, null);
                errors.AddRange(catalog.Data.Errors);
            }
            return (null, errors.Count > 0 ? "Kassa kataloqu: " + string.Join("; ", errors.Distinct()) : null);
        }

        /// <summary>Ambassador kateqoriya sinxronu kimi: ayrıca scope, xəta udulur (Walvero-da artıq yadda saxlanılıb).</summary>
        private async Task<PosCategorySyncResult> RunCategorySyncSafeAsync(int tenantId, int customerId, Guid? oldId, Guid? newId, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IPosSyncService>();
                return await sync.SetCustomerGuestCategoryAsync(tenantId, customerId, oldId, newId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Customer iiko discount category sync failed. CustomerId={CustomerId}, TenantId={TenantId}", customerId, tenantId);
                return new PosCategorySyncResult { AllOk = false, Notes = { ex.Message } };
            }
        }

        /// <summary>Wallet kartında «Endirim N%» sahəsi dərhal yenilənsin — fire-and-forget, cavabı gecikdirmir.</summary>
        private void EnqueuePassRefresh(int tenantId, int customerId)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var refresher = scope.ServiceProvider.GetRequiredService<ITenantSuspensionPassRefresher>();
                    await refresher.RefreshCustomerPassesAsync(tenantId, customerId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Customer pass refresh after discount change failed. CustomerId={CustomerId}, TenantId={TenantId}", customerId, tenantId);
                }
            });
        }

        private static string DescribeSync(PosCategorySyncResult r, bool assigned)
        {
            if (r.NoIntegration) return "Yadda saxlanıldı (kassa inteqrasiyası yoxdur).";
            if (r.NoMapping) return "Yadda saxlanıldı. Müştəri kassada hələ yoxdur — kassa sinxronu edin, kateqoriya avtomatik yazılacaq.";
            if (!r.AllOk) return "Yadda saxlanıldı, kassa xətası: " + string.Join("; ", r.Notes);
            return assigned ? "Kassaya yazıldı." : "Kassadan silindi.";
        }
    }
}
