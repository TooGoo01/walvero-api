using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IAboutRepository : IGenericRepository<About>
    {
        Task<IEnumerable<About>> GetAbouts();
        Task<IEnumerable<About>> GetActiveAbouts();
        Task<IEnumerable<About>> GetAllAbouts();
        Task<About> GetAbout(int id);
        Task<About> GetAbout();
        Task<About> GetLastAbout();
    }
}
