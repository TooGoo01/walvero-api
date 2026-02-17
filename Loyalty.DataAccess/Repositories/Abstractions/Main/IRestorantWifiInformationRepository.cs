using Loyalty.DataAccess.Entities.Main;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IRestorantWifiInformationRepository : IGenericRepository<RestorantWifiInformation>
    {
        Task<RestorantWifiInformation?> GetByIdWithTenantAsync(int id);
        Task<IEnumerable<RestorantWifiInformation>> GetByTenantIdAsync(int tenantId);
        Task<IEnumerable<RestorantWifiInformation>> GetAllWithTenantAsync();
    }
}
