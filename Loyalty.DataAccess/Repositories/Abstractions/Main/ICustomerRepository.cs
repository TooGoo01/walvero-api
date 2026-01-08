using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ICustomerRepository : IGenericRepository<Customer>
    {
        Task<Customer?> GetByEmailAsync(int tenantId, string email, CancellationToken ct = default);
        Task<Customer?> GetByPhoneAsync(int tenantId, string phone, CancellationToken ct = default);
        Task<Customer?> GetByWalveroIdAsync(int tenantId, long walveroId, CancellationToken ct = default);
        Task<List<Customer>> SearchAsync(int tenantId, string? q, int skip, int take, CancellationToken ct = default);
        Task<List<Customer>> GetByTenant(int tenantId, int? userId);
        Task<List<Customer>> GetByUserId(int userId);
    }
}
