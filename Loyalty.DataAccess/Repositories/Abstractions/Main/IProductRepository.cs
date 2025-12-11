using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IProductRepository : IGenericRepository<Product>
    {
        Task<IEnumerable<Product>> GetAll();
        Task<IEnumerable<Product>> GetActive();
        Task<IEnumerable<Product>> GetAllAbouts();
        Task<Product> GetById(int id);
        Task<Product> GetLast();
    }
}
