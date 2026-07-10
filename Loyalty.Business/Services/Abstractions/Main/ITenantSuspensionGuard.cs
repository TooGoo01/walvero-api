namespace Loyalty.Business.Services.Abstractions.Main
{
    public readonly record struct TenantSuspensionState(bool IsSuspended, string? Message);

    /// <summary>
    /// Tenant-ın ödəniş suspensiyası vəziyyətini cache-lənmiş oxuyur.
    /// Pass update path-lərində (redeemer-lər) istifadə olunur — Tenant nav yüklənməyən yerlərdə.
    /// </summary>
    public interface ITenantSuspensionGuard
    {
        Task<TenantSuspensionState> GetAsync(int tenantId, CancellationToken ct = default);
    }
}
