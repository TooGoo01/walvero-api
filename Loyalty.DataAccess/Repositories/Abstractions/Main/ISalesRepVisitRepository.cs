using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ISalesRepVisitRepository
    {
        Task<SalesRepVisit?> GetByIdAsync(int id, CancellationToken ct = default);

        Task<List<SalesRepVisit>> ListAsync(
            int? status,
            bool? remindOnly,
            string? searchText,
            CancellationToken ct = default);
    }
}
