using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IAppVersionRepository : IGenericRepository<AppVersionConfig>
    {
        Task<AppVersionConfig> GetLatestByPlatform(string Platform, string CurrentVersion);
        Task<AppVersionConfig?> GetActiveByPlatformAsync(string platform, CancellationToken ct = default);
        Task<IEnumerable<AppVersionConfig>> GetActiveList();
        Task<IEnumerable<AppVersionConfig>> GetAllList();
        Task<AppVersionConfig> GetById(int id);
    }
}
