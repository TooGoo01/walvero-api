using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IRewardRepository : IGenericRepository<Reward>
    {
        Task<IEnumerable<Reward>> GetByProgramIdAsync(int programId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Reward>> GetActiveRewardsByProgramIdAsync(int programId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Reward>> GetAvailableRewardsByPointsAsync(int programId, int points, CancellationToken cancellationToken = default);
    }
}
