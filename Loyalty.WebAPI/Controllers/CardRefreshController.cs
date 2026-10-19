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

        // programId: yalnız həmin proqramın kartları (ambassador kartı ayrıca dizayndır); null → bütün tenant
        private Task<int> CountActiveWalletCardsAsync(int tenantId, int? programId, CancellationToken ct) =>
            _db.LoyaltyCards.AsNoTracking()
                .Where(c => c.TenantId == tenantId
                         && c.IsActive
                         && c.Status == CardStatus.Active
                         && (c.IsAndroid || c.IsIOS)
                         && (programId == null || c.LoyaltyProgramId == programId))
                .CountAsync(ct);

        private static void EnqueueRefresh(int tenantId, int? programId) =>
            BackgroundJob.Enqueue<ITenantSuspensionPassRefresher>(
                x => x.RefreshProgramPassesAsync(tenantId, programId, CancellationToken.None));

        // ───────────── Super admin (per-tenant) ─────────────

        /// <summary>Super admin: tenant-ın aktiv wallet kart sayı (təsdiq üçün).</summary>
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        public async Task<IActionResult> Count([FromQuery] int tenantId, [FromQuery] int? programId, CancellationToken ct)
        {
            if (tenantId <= 0) return Ok(new ServiceResult(false, "Tenant tapılmadı."));
            var count = await CountActiveWalletCardsAsync(tenantId, programId, ct);
            return Ok(new ServiceResult(true, new { count }));
        }

        /// <summary>Super admin: tenant-ın bütün kartlarına dizayn update push (background).</summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        public async Task<IActionResult> Refresh([FromQuery] int tenantId, [FromQuery] int? programId, CancellationToken ct)
        {
            if (tenantId <= 0) return Ok(new ServiceResult(false, "Tenant tapılmadı."));
            var count = await CountActiveWalletCardsAsync(tenantId, programId, ct);
            EnqueueRefresh(tenantId, programId);
            return Ok(new ServiceResult(true, new { count }));
        }

        // ───────────── Tenant admin (öz tenant-ı) ─────────────

        /// <summary>Tenant admin: öz tenant-ının aktiv wallet kart sayı.</summary>
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> MyCount([FromQuery] int? programId, CancellationToken ct)
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue) return Ok(new ServiceResult(false, "Tenant tapılmadı."));
            var count = await CountActiveWalletCardsAsync(tenantId.Value, programId, ct);
            return Ok(new ServiceResult(true, new { count }));
        }

        /// <summary>Tenant admin: öz tenant kartlarına dizayn update push (background).</summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> RefreshMine([FromQuery] int? programId, CancellationToken ct)
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue) return Ok(new ServiceResult(false, "Tenant tapılmadı."));
            var count = await CountActiveWalletCardsAsync(tenantId.Value, programId, ct);
            EnqueueRefresh(tenantId.Value, programId);
            return Ok(new ServiceResult(true, new { count }));
        }
    }
}
