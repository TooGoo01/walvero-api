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

        /// <summary>Yalnız bir proqramın kartları (məs. ambassador kartının dizaynı dəyişəndə). programId null → bütün tenant.</summary>
        Task RefreshProgramPassesAsync(int tenantId, int? programId, CancellationToken ct = default);
    }
}
