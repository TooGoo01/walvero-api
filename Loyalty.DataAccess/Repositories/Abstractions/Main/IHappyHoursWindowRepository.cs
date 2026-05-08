using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IHappyHoursWindowRepository : IGenericRepository<HappyHoursWindow>
    {
        Task<List<HappyHoursWindow>> GetByProgramAsync(int loyaltyProgramId, CancellationToken ct = default);
        Task DeleteByProgramAsync(int loyaltyProgramId, CancellationToken ct = default);
    }
}
