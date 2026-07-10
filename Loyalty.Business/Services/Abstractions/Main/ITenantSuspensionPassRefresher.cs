namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Suspend/restore anında tenant-ın bütün Google/Apple Wallet pass-larına
    /// yeniləmə push edir. Bloklanmış tenant-da təbii pass yeniləməsi (redeem/earn)
    /// olmadığı üçün bu push kartın dəyişməsinin yeganə yoludur.
    /// </summary>
    public interface ITenantSuspensionPassRefresher
    {
        Task RefreshTenantPassesAsync(int tenantId, CancellationToken ct = default);
    }
}
