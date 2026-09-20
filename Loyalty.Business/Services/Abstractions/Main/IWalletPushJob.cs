namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Apple/Google Wallet pass yeniləməsini arxa fonda (Hangfire) icra edir.
    /// iiko webhook cavabı gözlətməsin deyə earn commit-dən sonra enqueue olunur.
    /// </summary>
    public interface IWalletPushJob
    {
        Task RunAsync(int cardId, int loyaltyProgramId, decimal delta);
    }
}
