using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Loyalty.DataAccess.Context;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class CustomerSyncService : ICustomerSyncService
    {
        private readonly ITenantIntegrationRepository _tenantIntegrationRepo;
        private readonly ITenantIntegrationVenueRepository _venueRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly ILoyaltyCardRepository _loyaltyCardRepo;
        private readonly IPosCustomerMappingRepository _mappingRepo;
        private readonly IMonyoPosApiClient _monyoClient;
        private readonly IIikoPosApiClient _iikoClient;
        private readonly ISensitiveDataProtector _protector;
        private readonly IUnitOfWork _uow;
        private readonly LoyaltyWalletDbContext _db;
        private readonly ILogger<CustomerSyncService> _logger;

        public CustomerSyncService(
            ITenantIntegrationRepository tenantIntegrationRepo,
            ITenantIntegrationVenueRepository venueRepo,
            ICustomerRepository customerRepo,
            ILoyaltyCardRepository loyaltyCardRepo,
            IPosCustomerMappingRepository mappingRepo,
            IMonyoPosApiClient monyoClient,
            IIikoPosApiClient iikoClient,
            ISensitiveDataProtector protector,
            IUnitOfWork uow,
            LoyaltyWalletDbContext db,
            ILogger<CustomerSyncService> logger)
        {
            _tenantIntegrationRepo = tenantIntegrationRepo;
            _venueRepo = venueRepo;
            _customerRepo = customerRepo;
            _loyaltyCardRepo = loyaltyCardRepo;
            _mappingRepo = mappingRepo;
            _monyoClient = monyoClient;
            _iikoClient = iikoClient;
            _protector = protector;
            _uow = uow;
            _db = db;
            _logger = logger;
        }

        public async Task<CustomerSyncResult> SyncCustomerToPosAsync(int tenantId, int customerId, CancellationToken ct = default)
        {
            var integration = await _tenantIntegrationRepo.GetActiveByTenantAsync(tenantId, ct);
            if (integration == null || string.IsNullOrEmpty(integration.ApiTokenEncrypted))
                return CustomerSyncResult.Fail();

            var apiToken = _protector.Decrypt(integration.ApiTokenEncrypted);
            if (string.IsNullOrEmpty(apiToken))
                return CustomerSyncResult.Fail();

            // iiko per-filial tenant-larda org id-lər venue-lardadır, inteqrasiyanın öz ExternalVenueId-si boş ola bilər;
            // iiko yolu venue/legacy seçimini özü edir. Monyo üçün ExternalVenueId məcburidir.
            var isIiko = integration.IntegrationType?.Source == "iiko_pos";
            if (!isIiko && string.IsNullOrEmpty(integration.ExternalVenueId))
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

            var name = $"{customer.FirstName} {customer.LastName}".Trim();
            if (string.IsNullOrEmpty(name)) name = customer.PhoneNumber;

            var source = integration.IntegrationType?.Source;

            switch (source)
            {
                case "iiko_pos":
                    return await SyncCustomerToIikoAsync(integration, apiToken, customer, customerId, barcode, name, ct);
                case "monyo_pos":
                default:
                    return await SyncCustomerToMonyoAsync(integration, apiToken, customer, customerId, barcode, name, ct);
            }
        }

        private async Task<CustomerSyncResult> SyncCustomerToMonyoAsync(
            TenantIntegration integration, string apiToken, Customer customer,
            int customerId, string? barcode, string name, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(integration.ApiBaseUrl))
                return CustomerSyncResult.Fail();

            var existingMapping = await _mappingRepo.GetByTenantIntegrationAndCustomerAsync(integration.Id, customerId, ct);
            var monyoId = existingMapping?.ExternalCustomerId;

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
                var resp = await _monyoClient.UpdateCustomersAsync(integration.ApiBaseUrl, apiToken, integration.ExternalVenueId!, updateDtos, ct);
                return resp != null && resp.Length > 0 ? CustomerSyncResult.Ok(null, barcode) : CustomerSyncResult.Fail();
            }

            var addDtos = new[] { new MonyoCustomerDto
            {
                Name = name,
                Phone = null,
                CustomerCode = customer.Id.ToString(),
                Barcode = barcode
            }};
            var addResp = await _monyoClient.AddCustomersAsync(integration.ApiBaseUrl, apiToken, integration.ExternalVenueId!, addDtos, ct);
            if (addResp == null || addResp.Length == 0)
                return CustomerSyncResult.Fail();

            var r = addResp[0];
            await CreateMappingAsync(integration.Id, null, r.Id.ToString(), customerId, true, ct);
            return CustomerSyncResult.Ok(null, barcode);
        }

        private async Task<CustomerSyncResult> SyncCustomerToIikoAsync(
            TenantIntegration integration, string apiToken, Customer customer,
            int customerId, string? barcode, string name, CancellationToken ct)
        {
            // apiToken = iiko apiLogin; v2 app-auth (appId/clientSecret optional)
            var clientSecret = string.IsNullOrEmpty(integration.ApiSecretEncrypted) ? null : _protector.Decrypt(integration.ApiSecretEncrypted);
            var accessToken = await _iikoClient.GetAccessTokenAsync(apiToken, integration.ApiAppId, clientSecret, ct: ct);
            if (string.IsNullOrEmpty(accessToken))
                return CustomerSyncResult.Fail();

            // barcode "walveroId,actualBarcode" formatında gələ bilər — parse et
            string? walveroIdStr = null;
            string? actualBarcode = barcode;
            if (!string.IsNullOrEmpty(barcode) && barcode.Contains(','))
            {
                var parts = barcode.Split(',', 2);
                walveroIdStr = parts[0];
                actualBarcode = parts[1];
            }

            // Per-branch: hər venue (org) üçün ayrıca müştəri yarat. Venue yoxdursa — legacy tək-org (parent ExternalVenueId).
            var venues = await _venueRepo.GetActiveByIntegrationAsync(integration.Id, ct);
            var anyOk = false;

            if (venues.Count > 0)
            {
                foreach (var venue in venues)
                {
                    if (!Guid.TryParse(venue.ExternalVenueId, out var vOrgId))
                    {
                        _logger.LogWarning("iiko sync: venue ExternalVenueId GUID deyil: {VenueId} (venueRowId={RowId})", venue.ExternalVenueId, venue.Id);
                        continue;
                    }
                    var programId = string.IsNullOrEmpty(venue.ExternalProgramId) ? integration.ExternalProgramId : venue.ExternalProgramId;
                    var ok = await SyncCustomerToOneOrgAsync(accessToken, vOrgId, integration.Id, venue.Id, programId, customer, customerId, actualBarcode, walveroIdStr, ct);
                    anyOk = anyOk || ok;
                }
            }
            else if (!string.IsNullOrEmpty(integration.ExternalVenueId) && Guid.TryParse(integration.ExternalVenueId, out var orgId))
            {
                anyOk = await SyncCustomerToOneOrgAsync(accessToken, orgId, integration.Id, null, integration.ExternalProgramId, customer, customerId, actualBarcode, walveroIdStr, ct);
            }

            return anyOk ? CustomerSyncResult.Ok(null, barcode) : CustomerSyncResult.Fail();
        }

        /// <summary>Bir iiko org-da müştəri yarat/yenilə + walveroId kartı + program enroll + mapping (per-venue). venueId null = legacy tək-org.</summary>
        private async Task<bool> SyncCustomerToOneOrgAsync(
            string accessToken, Guid orgId, int integrationId, int? venueId, string? programId,
            Customer customer, int customerId, string? actualBarcode, string? walveroIdStr, CancellationToken ct)
        {
            var existingMapping = venueId.HasValue
                ? await _mappingRepo.GetByVenueAndCustomerAsync(venueId.Value, customerId, ct)
                : await _mappingRepo.GetByTenantIntegrationAndCustomerAsync(integrationId, customerId, ct);

            var iikoCustomer = new IikoCustomerDto
            {
                Id = existingMapping != null && Guid.TryParse(existingMapping.ExternalCustomerId, out var exId) ? exId : (Guid?)null,
                Name = customer.FirstName,
                Surname = customer.LastName,
                CardTrack = actualBarcode,
                CardNumber = actualBarcode,
                Birthday = customer.DateOfBirth?.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };

            var result = await _iikoClient.CreateOrUpdateCustomerAsync(accessToken, orgId, iikoCustomer, ct);
            if (result == null)
            {
                _logger.LogWarning("iiko sync: CreateOrUpdate failed. customerId={CustomerId} orgId={OrgId}", customerId, orgId);
                return false;
            }

            // İkinci kart (walveroId) əlavə et — ayrı kart olaraq
            if (!string.IsNullOrEmpty(walveroIdStr))
                await _iikoClient.AddCustomerCardAsync(accessToken, orgId, result.Id, walveroIdStr, walveroIdStr, ct);

            if (existingMapping == null)
            {
                var enrolled = await TryEnrollInIikoProgramAsync(programId, accessToken, orgId, result.Id, customerId, ct);
                await CreateMappingAsync(integrationId, venueId, result.Id.ToString(), customerId, enrolled, ct);
            }
            else if (!existingMapping.IsEnrolledInProgram)
            {
                var enrolled = await TryEnrollInIikoProgramAsync(programId, accessToken, orgId, result.Id, customerId, ct);
                if (enrolled)
                {
                    existingMapping.IsEnrolledInProgram = true;
                    _mappingRepo.Update(existingMapping);
                    await _uow.CommitAsync(ct);
                }
            }
            return true;
        }

        /// <summary>
        /// iiko proqramına əlavə et. programId yoxdursa true qayıdır (heç bir iş yoxdur).
        /// Çağrı edilibsə HTTP nəticəsi qaytarılır. Bad GUID — false + warning.
        /// </summary>
        private async Task<bool> TryEnrollInIikoProgramAsync(
            string? programIdStr, string accessToken, Guid orgId, Guid iikoCustomerId,
            int customerId, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(programIdStr))
                return true; // proqram konfiqurasiya edilməyib — enroll lazım deyil

            if (!Guid.TryParse(programIdStr, out var programId))
            {
                _logger.LogWarning(
                    "iiko ExternalProgramId is not a valid GUID; skipping customer_program/add. value={Value} customerId={CustomerId}",
                    programIdStr, customerId);
                return false;
            }

            var added = await _iikoClient.AddCustomerToProgramAsync(accessToken, orgId, iikoCustomerId, programId, ct);
            _logger.LogInformation(
                "iiko customer_program/add result={Result} customerId={CustomerId} iikoCustomerId={IikoCustomerId} programId={ProgramId} orgId={OrgId}",
                added, customerId, iikoCustomerId, programId, orgId);
            return added;
        }

        private async Task CreateMappingAsync(int integrationId, int? venueId, string externalId, int customerId, bool isEnrolledInProgram, CancellationToken ct)
        {
            var mapping = new PosCustomerMapping
            {
                TenantIntegrationId = integrationId,
                TenantIntegrationVenueId = venueId,
                ExternalCustomerId = externalId,
                CustomerId = customerId,
                IsEnrolledInProgram = isEnrolledInProgram,
                RegUserId = 1,
                RegDate = DateTime.UtcNow,
                IsActive = true
            };
            await _mappingRepo.AddAsync(mapping);
            await _uow.CommitAsync(ct);
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
