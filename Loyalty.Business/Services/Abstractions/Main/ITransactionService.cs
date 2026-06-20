using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ITransactionService
    {
        Task<ServiceResult> Get(int id);
        Task<ServiceResult> GetByProgramId(int id);
        Task<ServiceResult> GetAll();
        Task<ServiceResult> GetActive();
        Task<ServiceResult> GetLast();
        Task<ServiceResult> GetByTenantIdAndDate(int tenantId, DateOnly startDate, DateOnly endDate, int? branchId = null);
        Task<ServiceResult> GetByCustomerId(int customerId, CancellationToken ct = default);

        /// <summary>
        /// JWT-dəki user-in (applicationUserId) bütün tenant-lar üzrə təxsəl-edilməmiş tx tarixçəsi.
        /// Customer mobile app üçün.
        /// </summary>
        Task<ServiceResult> GetMyTransactionsAsync(int applicationUserId, CancellationToken ct = default);
    }
}
