namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Kart dizayneri feature flag-ları. Program.cs başlanğıcda konfiqurasiyadan oxuyub set edir
    /// (`CardDesign:GoogleClassUpdateEnabled`, default false — Faza 0 sınaqları bitənə qədər real Google class PATCH bağlıdır).
    /// </summary>
    public static class CardDesignFeatureFlags
    {
        /// <summary>true → admin saxlayanda / refresh-də real Google Wallet class (programName/issuer/rəng/logo) PATCH olunur.</summary>
        public static bool GoogleClassUpdateEnabled { get; set; } = false;
    }
}
