using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using Loyalty.DataAccess.Context;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class CustomerSyncService : ICustomerSyncService
    {
        private readonly ITenantIntegrationRepository _tenantIntegrationRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly ILoyaltyCardRepository _loyaltyCardRepo;
        private readonly IPosCustomerMappingRepository _mappingRepo;
        private readonly IMonyoPosApiClient _monyoClient;
        private readonly ISensitiveDataProtector _protector;
        private readonly IUnitOfWork _uow;
        private readonly LoyaltyWalletDbContext _db;

        public CustomerSyncService(
            ITenantIntegrationRepository tenantIntegrationRepo,
            ICustomerRepository customerRepo,
            ILoyaltyCardRepository loyaltyCardRepo,
            IPosCustomerMappingRepository mappingRepo,
            IMonyoPosApiClient monyoClient,
            ISensitiveDataProtector protector,
            IUnitOfWork uow,
            LoyaltyWalletDbContext db)
        {
            _tenantIntegrationRepo = tenantIntegrationRepo;
            _customerRepo = customerRepo;
            _loyaltyCardRepo = loyaltyCardRepo;
            _mappingRepo = mappingRepo;
            _monyoClient = monyoClient;
            _protector = protector;
            _uow = uow;
            _db = db;
        }

        public async Task<CustomerSyncResult> SyncCustomerToPosAsync(int tenantId, int customerId, CancellationToken ct = default)
        {
            var integration = await _tenantIntegrationRepo.GetActiveByTenantAsync(tenantId, ct);
            if (integration == null || string.IsNullOrEmpty(integration.ApiBaseUrl) || string.IsNullOrEmpty(integration.ApiTokenEncrypted))
                return CustomerSyncResult.Fail();

            var apiToken = _protector.Decrypt(integration.ApiTokenEncrypted);
            if (string.IsNullOrEmpty(apiToken) || string.IsNullOrEmpty(integration.ExternalVenueId))
                return CustomerSyncResult.Fail();

            var customer = await _customerRepo.GetByIdAsync(customerId, ct);
            if (customer == null || customer.TenantId != tenantId)
                return CustomerSyncResult.Fail();

            var cards = await _loyaltyCardRepo.ListByCustomerAsync(tenantId, customerId, ct);
            var barcode = cards?.Where(c => !string.IsNullOrEmpty(c.BarcodeValue)).Select(c => c.BarcodeValue).FirstOrDefault() ?? customer.CardNumber;

            // WalveroId-ni ApplicationUser-dən al və barcode-a əlavə et: "walveroId,barcode"
            if (customer.applicationUserId.HasValue)
            {
                var walveroId = await _db.Users
                    .Where(u => u.Id == customer.applicationUserId.Value)
                    .Select(u => u.WalveroId)
                    .FirstOrDefaultAsync(ct);

                if (walveroId.HasValue)
                    barcode = $"{walveroId.Value},{barcode}";
            }

            var existingMapping = await _mappingRepo.GetByTenantIntegrationAndCustomerAsync(integration.Id, customerId, ct);
            var monyoId = existingMapping?.ExternalCustomerId;

            var name = $"{customer.FirstName} {customer.LastName}".Trim();
            if (string.IsNullOrEmpty(name)) name = customer.PhoneNumber;

            if (!string.IsNullOrEmpty(monyoId))
            {
                var updateDtos = new[] { new MonyoCustomerUpdateDto
                {
                    MonyoId = monyoId,
                    Name = name,
                    Phone = null,
                    CustomerCode = customer.Id.ToString(),
                    Barcode = barcode
                }};
                var resp = await _monyoClient.UpdateCustomersAsync(integration.ApiBaseUrl, apiToken, integration.ExternalVenueId, updateDtos, ct);
                return resp != null && resp.Length > 0 ? CustomerSyncResult.Ok(null, barcode) : CustomerSyncResult.Fail();
            }

            var addDtos = new[] { new MonyoCustomerDto
            {
                Name = name,
                Phone = null,
                CustomerCode = customer.Id.ToString(),
                Barcode = barcode
            }};
            var addResp = await _monyoClient.AddCustomersAsync(integration.ApiBaseUrl, apiToken, integration.ExternalVenueId, addDtos, ct);
            if (addResp == null || addResp.Length == 0)
                return CustomerSyncResult.Fail();

            var r = addResp[0];
            var mapping = new PosCustomerMapping
            {
                TenantIntegrationId = integration.Id,
                ExternalCustomerId = r.Id.ToString(),
                CustomerId = customerId,
                RegUserId = 1,
                RegDate = DateTime.UtcNow,
                IsActive = true
            };
            await _mappingRepo.AddAsync(mapping);
            await _uow.CommitAsync(ct);
            return CustomerSyncResult.Ok(null, barcode);
        }

        public async Task<int> SyncAllCustomersToPosAsync(int tenantId, CancellationToken ct = default)
        {
            var integration = await _tenantIntegrationRepo.GetActiveByTenantAsync(tenantId, ct);
            if (integration == null)
                return 0;

            var customers = await _customerRepo.GetAllAsync(c => c.TenantId == tenantId && c.IsActive);
            var count = 0;
            foreach (var c in customers)
            {
                var result = await SyncCustomerToPosAsync(tenantId, c.Id, ct);
                if (result.Success)
                    count++;
            }
            return count;
        }
    }
}
