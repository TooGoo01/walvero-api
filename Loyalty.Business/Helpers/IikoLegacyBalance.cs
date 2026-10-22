namespace Loyalty.Business.Helpers
{
    /// <summary>Telefonla bağlanan iiko qonağının köhnə balansı üçün pure qaydalar (DB-siz, test olunur).</summary>
    public static class IikoLegacyBalance
    {
        /// <summary>Transaction.OrderId — eyni iiko qonağı + cüzdan balansı bir dəfə əlavə olunsun.</summary>
        public static string OrderRef(string iikoCustomerId, string? walletId)
            => $"iiko-legacy:{iikoCustomerId.Trim().ToLowerInvariant()}:{(walletId ?? "-").Trim().ToLowerInvariant()}";

        /// <summary>Köhnə balans hələ Walvero-ya yazılmayıb — bu müddətdə iiko-ya balans push olunmamalıdır (X chargeoff olunar).</summary>
        public static bool IsPending(decimal? legacyBalance, DateTime? importedAt)
            => legacyBalance > 0 && importedAt == null;

        /// <summary>Tapılma anında yazılacaq məbləğ: mənfi/null → 0, 2 onluq.</summary>
        public static decimal Normalize(decimal? balance)
            => balance is > 0 ? Math.Round(balance.Value, 2) : 0m;
    }
}
