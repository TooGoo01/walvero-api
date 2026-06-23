using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IPosLoyaltyDiscountTypeRepository : IGenericRepository<PosLoyaltyDiscountType>
    {
        Task<IReadOnlyList<PosLoyaltyDiscountType>> GetByTenantIntegrationIdAsync(int tenantIntegrationId, CancellationToken ct = default);
    }
}
