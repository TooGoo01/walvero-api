using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Kart keçidində (ambassador et / çıxar) müştərinin telefonundakı wallet pass-ı yeni aktiv karta "ötürür" —
    /// müştəri yeni kart əlavə etmədən köhnə pass yeni kartın dizaynını/balansını göstərir.
    ///
    /// Apple: pass serial-ı (qeydiyyatlı cihazlar serial-a bağlıdır) bağlanan kartdan aktiv karta köçürülür;
    ///   bağlanan karta unikal "~" serial verilir ki, onun voided build-i cihazdakı pass-a toxunmasın.
    /// Google: obyekt ID kart ID-dən törəyir (köçürülə bilmir) — aktiv kartın öz obyekti yoxdursa (IsAndroid=false)
    ///   push bağlanmış qardaş kartın obyektinə gedir (host), host INACTIVE edilmir.
    /// </summary>
    public sealed class WalletPassHandover : IWalletPassHandover
    {
        private readonly LoyaltyWalletDbContext _db;
        private readonly ILogger<WalletPassHandover> _logger;

        public WalletPassHandover(LoyaltyWalletDbContext db, ILogger<WalletPassHandover> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<bool> EnsureAppleHandoverAsync(LoyaltyCard active, CancellationToken ct = default)
        {
            if (!active.IsActive) return false;
            if (await HasActiveRegistrationsAsync(active.AppleSerial, ct)) return false; // öz pass-ı cihazdadır

            // Bağlanmış qardaş kartlar (ən yenisi önə) — cihazda qeydiyyatlı serial-ı olan ilki
            var siblings = await _db.Set<LoyaltyCard>()
                .Where(SwitchSiblingOf(active))
                .Where(c => c.AppleSerial != null)
                .OrderByDescending(c => c.EditDate ?? c.RegDate)
                .ToListAsync(ct);

            foreach (var source in siblings)
            {
                if (!await HasActiveRegistrationsAsync(source.AppleSerial, ct)) continue;

                var serial = source.AppleSerial!;
                source.AppleSerial = $"{serial}~{active.Id}"; // cihazsız, unikal — voided build cihaz pass-ına getmir
                active.AppleSerial = serial;
                active.AppleAuthToken = source.AppleAuthToken ?? active.AppleAuthToken;
                active.IsIOS = true;

                var regs = await _db.Set<PassDeviceRegistration>()
                    .Where(r => r.SerialNumber == serial && r.IsActive)
                    .ToListAsync(ct);
                foreach (var r in regs) r.LoyaltyCardId = active.Id;

                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Wallet handover (Apple): Serial={Serial} CardId {From} → {To}, Devices={Devices}",
                    serial, source.Id, active.Id, regs.Count);
                return true;
            }
            return false;
        }

        public async Task<LoyaltyCard?> FindGoogleHostAsync(LoyaltyCard active, CancellationToken ct = default)
        {
            if (!active.IsActive || active.IsAndroid) return null; // öz Google obyekti var
            return await _db.Set<LoyaltyCard>().AsNoTracking()
                .Where(SwitchSiblingOf(active))
                .Where(c => c.IsAndroid)
                .OrderByDescending(c => c.EditDate ?? c.RegDate)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<bool> IsGoogleHostForActiveCardAsync(LoyaltyCard closed, CancellationToken ct = default)
        {
            if (closed.IsActive || !closed.IsAndroid) return false;
            var active = await _db.Set<LoyaltyCard>().AsNoTracking()
                .Where(c => c.TenantId == closed.TenantId && c.CustomerId == closed.CustomerId && c.IsActive)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync(ct);
            if (active == null) return false;
            var host = await FindGoogleHostAsync(active, ct);
            return host?.Id == closed.Id;
        }

        /// <summary>
        /// Kart keçidində bağlanmış qardaş kart: eyni müştəri/tenant, bağlı (Cancelled) və BAŞQA proqramda.
        /// Eyni proqram şərti silinib yenidən qeydiyyatdan keçən kartı (UserCardService.Delete) kənarda saxlayır.
        /// EF ifadə ağacına çevrilir (static, closure yalnız active sahələri).
        /// </summary>
        private static System.Linq.Expressions.Expression<Func<LoyaltyCard, bool>> SwitchSiblingOf(LoyaltyCard active)
        {
            var tenantId = active.TenantId;
            var customerId = active.CustomerId;
            var id = active.Id;
            var programId = active.LoyaltyProgramId;
            return c => c.TenantId == tenantId && c.CustomerId == customerId && c.Id != id
                        && !c.IsActive && c.Status == CardStatus.Cancelled && c.LoyaltyProgramId != programId;
        }

        private Task<bool> HasActiveRegistrationsAsync(string? serial, CancellationToken ct)
            => string.IsNullOrWhiteSpace(serial)
                ? Task.FromResult(false)
                : _db.Set<PassDeviceRegistration>().AnyAsync(r => r.SerialNumber == serial && r.IsActive, ct);
    }
}
