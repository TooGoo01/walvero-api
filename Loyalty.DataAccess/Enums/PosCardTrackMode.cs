namespace Loyalty.DataAccess.Enums
{
    /// <summary>
    /// iiko müştərisinə hansı identifikatorun ƏSAS kart track-i kimi göndəriləcəyi (TenantIntegration ayarı).
    /// Tam barkod həmişə əlavə track kimi də yazılır (skan işləsin).
    /// </summary>
    public enum PosCardTrackMode
    {
        /// <summary>Kartın tam barkodu (məs. 789567890655).</summary>
        FullBarcode = 1,
        /// <summary>App istifadəçisinin WalveroId-si (9 rəqəm); app-sız müştəridə tam barkoda düşür.</summary>
        WalveroId = 2,
        /// <summary>Barkodun son N rəqəmi (CardTrackDigits, 4–8) — kassir əl ilə yazır.</summary>
        LastDigits = 3
    }
}
