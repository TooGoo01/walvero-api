using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IPartnerRepository : IGenericRepository<Partner>
    {
        Task<IEnumerable<Partner>> GetAll();
        Task<IEnumerable<Partner>> GetActive();
        Task<IEnumerable<Partner>> GetAllAbouts();
        Task<Partner> GetById(int id);
        Task<Partner> GetLast();
    }
}
