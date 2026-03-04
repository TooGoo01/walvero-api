using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ICustomerEnrollmentService
    {
        Task<ServiceResult> StartEnrollmentAsync(
            AddCustomerWithCardDto dto,
          
            CancellationToken ct = default);
        Task<ServiceResult> GetTenantCustomers(
           int tenantId,string username,
           CancellationToken ct = default);
        Task<ServiceResult> GetByIdName(
         int customer, string username
        );
        Task<ServiceResult> VerifyEnrollmentAsync(VerifyCustomerOtpDto dto, CancellationToken ct = default);
        Task<ServiceResult> StartPublicEnrollmentAsync(PublicEnrollmentStartDto dto, CancellationToken ct = default);
        Task<ServiceResult> GetByUserId(int userId);
    }
}
