namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Müştərinin iiko cüzdanlarından hansına yazılsın (DB-siz, test olunur). Bir müştərinin bir neçə cüzdanı ola bilər
    /// (pims: Cashback + Ambassador) — proqramın PosWalletId-si varsa yalnız o; yoxdursa bugünkü davranış (birinci).
    /// </summary>
    public static class IikoWalletSelection
    {
        public enum Outcome
        {
            /// <summary>Uyğun cüzdan tapıldı.</summary>
            Found,
            /// <summary>Proqramın cüzdanı müştəridə yoxdur — müştəri həmin iiko proqramına yazılmalıdır.</summary>
            PreferredMissing,
            /// <summary>Müştərinin heç bir cüzdanı yoxdur.</summary>
            NoWallets
        }

        public static (Outcome Outcome, Guid? WalletId, decimal Balance) Pick(
            IReadOnlyList<(Guid Id, decimal Balance)> wallets, string? preferredWalletId)
        {
            if (Guid.TryParse(preferredWalletId, out var preferred))
            {
                foreach (var w in wallets)
                    if (w.Id == preferred) return (Outcome.Found, w.Id, w.Balance);
                // Başqa cüzdana yazmaq olmaz (məs. ambassador vahidləri keşbek cüzdanına düşməsin)
                return (Outcome.PreferredMissing, preferred, 0m);
            }
            return wallets.Count > 0
                ? (Outcome.Found, wallets[0].Id, wallets[0].Balance)
                : (Outcome.NoWallets, null, 0m);
        }

        /// <summary>
        /// iiko cüzdan güzgüsü (iiko_webhook) bu kartın balansını yazsınmı: proqramın cüzdanı təyin edilibsə yalnız o cüzdanın
        /// tranzaksiyası (ambassador-un keşbek cüzdanındakı 5% onun say balansını əvəz etməsin). Təyin edilməyibsə hamısı (bugünkü).
        /// </summary>
        public static bool MirrorAppliesToCard(string? programWalletId, string? transactionWalletId)
            => string.IsNullOrWhiteSpace(programWalletId)
               || string.Equals(programWalletId.Trim(), transactionWalletId?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
