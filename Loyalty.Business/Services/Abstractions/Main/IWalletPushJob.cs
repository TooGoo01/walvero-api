namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Apple/Google Wallet pass yeniləməsini arxa fonda (Hangfire) icra edir.
    /// iiko webhook cavabı gözlətməsin deyə earn commit-dən sonra enqueue olunur.
    /// </summary>
    public interface IWalletPushJob
    {
        Task RunAsync(int cardId, int loyaltyProgramId, decimal delta);

        /// <summary>Geri açılan kart: Google obyektini ACTIVE et, sonra adi push (Apple voided götürülür).</summary>
        Task ReactivateAsync(int cardId, int loyaltyProgramId);
    }
}
