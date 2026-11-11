using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>Kart keçidində telefondakı wallet pass-ı yeni aktiv karta ötürür (bax WalletPassHandover).</summary>
    public interface IWalletPassHandover
    {
        /// <summary>Aktiv kartın öz Apple pass-ı cihazda yoxdursa, bağlanmış qardaş kartın cihazdakı serial-ını ona köçürür. Köçürübsə true.</summary>
        Task<bool> EnsureAppleHandoverAsync(LoyaltyCard active, CancellationToken ct = default);

        /// <summary>Aktiv kartın öz Google obyekti yoxdursa, onu təmsil edən bağlanmış qardaş kart (host); yoxdursa null.</summary>
        Task<LoyaltyCard?> FindGoogleHostAsync(LoyaltyCard active, CancellationToken ct = default);

        /// <summary>Bağlanmış kartın Google obyekti müştərinin aktiv kartını təmsil edirmi (o halda INACTIVE edilməməlidir).</summary>
        Task<bool> IsGoogleHostForActiveCardAsync(LoyaltyCard closed, CancellationToken ct = default);
    }
}
