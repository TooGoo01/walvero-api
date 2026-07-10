namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Ödəniş suspensiyası zamanı müştəri kartlarında göstərilən mask dəyərləri.
    /// Data DB-də dəyişmir — yalnız read/pass-build qatında tətbiq olunur,
    /// ona görə restore = flag off, hər şey köhnə vəziyyətinə qayıdır.
    /// </summary>
    public static class TenantSuspensionMask
    {
        public const string DefaultMessage = "Ödəniş edilmədiyi üçün servis bloklanmışdır";

        public static string EffectiveMessage(string? tenantMessage)
            => string.IsNullOrWhiteSpace(tenantMessage) ? DefaultMessage : tenantMessage;
    }
}
