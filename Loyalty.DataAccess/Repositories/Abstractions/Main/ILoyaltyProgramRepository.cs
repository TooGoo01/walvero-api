using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ILoyaltyProgramRepository:IGenericRepository<LoyaltyProgram>
    {
        Task<LoyaltyProgram?> GetByCodeAsync(int tenantId, string code, CancellationToken ct = default);
        Task<LoyaltyProgram?> GetByIdNameAsync(int programId, CancellationToken ct = default);
        Task<LoyaltyProgram?> GetByIdTrackedAsync(int programId, CancellationToken ct = default);
        Task<List<LoyaltyProgram>> ListAsync(int tenantId, bool onlyActive = true, CancellationToken ct = default);
        Task<List<LoyaltyProgram>> GetActive(CancellationToken ct = default);
        Task<List<LoyaltyProgram>> GetByIdsAsync(IEnumerable<int> programIds, CancellationToken ct = default);
    }
}
