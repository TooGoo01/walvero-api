namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Tenant-ın aktivliyini yoxlayan mərkəzi guard. Deaktiv (IsActive=false) tenant-da
    /// heç bir əməliyyat (earn/redeem/scan/webhook/enrollment) getməməlidir.
    /// Nəticə qısa müddətli cache-lənir ki, hər əməliyyat DB-yə getməsin.
    /// </summary>
    public interface ITenantStatusGuard
    {
        /// <summary>True = tenant mövcuddur və aktivdir.</summary>
        Task<bool> IsActiveAsync(int tenantId, CancellationToken ct = default);
    }
}
