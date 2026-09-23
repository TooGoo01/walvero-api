using Hangfire;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// Kart dizaynı (template/branding) yeniləndikdən sonra tenant-ın bütün aktiv wallet
    /// kartlarına update push — dizayn telefonlarda yenilənsin, BALANS DƏYİŞMƏSİN.
    /// Reuse: <see cref="ITenantSuspensionPassRefresher.RefreshTenantPassesAsync"/> (delta=0, cari state re-push).
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CardRefreshController : ControllerBase
    {
        private readonly LoyaltyWalletDbContext _db;
        private readonly ITenantProvider _tenantProvider;

        public CardRefreshController(LoyaltyWalletDbContext db, ITenantProvider tenantProvider)
        {
            _db = db;
            _tenantProvider = tenantProvider;
        }

        private Task<int> CountActiveWalletCardsAsync(int tenantId, CancellationToken ct) =>
            _db.LoyaltyCards.AsNoTracking()
                .Where(c => c.TenantId == tenantId
                         && c.IsActive
                         && c.Status == CardStatus.Active
                         && (c.IsAndroid || c.IsIOS))
                .CountAsync(ct);

        private static void EnqueueRefresh(int tenantId) =>
            BackgroundJob.Enqueue<ITenantSuspensionPassRefresher>(
                x => x.RefreshTenantPassesAsync(tenantId, CancellationToken.None));

        // ───────────── Super admin (per-tenant) ─────────────

        /// <summary>Super admin: tenant-ın aktiv wallet kart sayı (təsdiq üçün).</summary>
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        public async Task<IActionResult> Count([FromQuery] int tenantId, CancellationToken ct)
        {
            if (tenantId <= 0) return Ok(new ServiceResult(false, "Tenant tapılmadı."));
            var count = await CountActiveWalletCardsAsync(tenantId, ct);
            return Ok(new ServiceResult(true, new { count }));
        }

        /// <summary>Super admin: tenant-ın bütün kartlarına dizayn update push (background).</summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        public async Task<IActionResult> Refresh([FromQuery] int tenantId, CancellationToken ct)
        {
            if (tenantId <= 0) return Ok(new ServiceResult(false, "Tenant tapılmadı."));
            var count = await CountActiveWalletCardsAsync(tenantId, ct);
            EnqueueRefresh(tenantId);
            return Ok(new ServiceResult(true, new { count }));
        }

        // ───────────── Tenant admin (öz tenant-ı) ─────────────

        /// <summary>Tenant admin: öz tenant-ının aktiv wallet kart sayı.</summary>
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> MyCount(CancellationToken ct)
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue) return Ok(new ServiceResult(false, "Tenant tapılmadı."));
            var count = await CountActiveWalletCardsAsync(tenantId.Value, ct);
            return Ok(new ServiceResult(true, new { count }));
        }

        /// <summary>Tenant admin: öz tenant kartlarına dizayn update push (background).</summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> RefreshMine(CancellationToken ct)
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue) return Ok(new ServiceResult(false, "Tenant tapılmadı."));
            var count = await CountActiveWalletCardsAsync(tenantId.Value, ct);
            EnqueueRefresh(tenantId.Value);
            return Ok(new ServiceResult(true, new { count }));
        }
    }
}
