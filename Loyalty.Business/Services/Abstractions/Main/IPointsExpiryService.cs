namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Kart balansının son istifadə tarixi (LoyaltyCard.ExpiresAt) keçəndə balansı sıfırlayır:
    /// Expired tranzaksiya + wallet pass push + iiko wallet chargeoff. iiko sync təsdiqlənəndə ExpiresAt null edilir;
    /// alınmasa tarix qalır və növbəti run yalnız sync-i təkrarlayır.
    /// </summary>
    public interface IPointsExpiryService
    {
        /// <summary>Hangfire recurring: vaxtı çatmış bütün kartlar (batch).</summary>
        Task RunAsync(CancellationToken ct = default);

        /// <summary>Tək kart üçün dərhal (admin keçmiş tarix qoyanda). Qaytarır: sıfırlanan xal.</summary>
        Task<decimal> ExpireCardAsync(int cardId, CancellationToken ct = default);

        /// <summary>Vaxtı keçmiş müddətli bonuslar (BonusGrant, məs. ad günü hədiyyəsi) — RunAsync-in içində də çağırılır.</summary>
        Task ExpireBonusGrantsAsync(CancellationToken ct = default);
    }
}
