using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IServiceRepository : IGenericRepository<Service>
    {
        Task<IEnumerable<Service>> GetAll();
        Task<IEnumerable<Service>> GetActive();
        Task<IEnumerable<Service>> GetAllAbouts();
        Task<Service> GetById(int id);
        Task<Service> GetLast();
    }
}
