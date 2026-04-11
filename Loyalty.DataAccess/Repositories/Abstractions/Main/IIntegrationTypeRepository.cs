using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IIntegrationTypeRepository : IGenericRepository<IntegrationType>
    {
        Task<IntegrationType?> GetBySourceAsync(string source, CancellationToken ct = default);
        Task<IReadOnlyList<IntegrationType>> GetAllActiveAsync(CancellationToken ct = default);
        Task AddAndSaveAsync(IntegrationType entity, CancellationToken ct = default);
        Task UpdateAndSaveAsync(IntegrationType entity, CancellationToken ct = default);
    }
}
