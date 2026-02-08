using AutoMapper;
using Azure.Core;
using Google.Apis.Walletobjects.v1.Data;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Sequences;
using Loyalty.Business.Services.Abstractions.Users;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Implementations.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Data.Entity.Core.Metadata.Edm;
using static System.Net.WebRequestMethods;
using DateTime = System.DateTime;
namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class CustomerEnrollmentService : ICustomerEnrollmentService
    {
        private readonly ITenantProvider _tp;
        private readonly ICustomerRepository _customerRepo;
        private readonly ILoyaltyProgramRepository _programRepo;
        private readonly ILoyaltyCardRepository _cardRepo;
        private readonly ILoyaltyCardService _cardSvc; // 6-rəqəmli barcode unikal generatoru burdadır
        private readonly IWalletCredentialService _credSvc;
        private readonly IGoogleWalletServiceFactory _googleFactory;
        private readonly IAppleWalletService _appleService; // .pkpass URL qaytaran servis
        private readonly IUnitOfWork _uow;
        private readonly IAppEnvironment _env;
        private readonly ITenantBrandingRepository _tenantBrandingRepository;
        private readonly IMapper _mapper;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICardNumberGenerator _cardNumberGenerator;
        private readonly ILogger<CustomerEnrollmentService> _logger;
        public CustomerEnrollmentService(
            ITenantProvider tp,
            ICustomerRepository customerRepo,
            ILoyaltyProgramRepository programRepo,
            ILoyaltyCardRepository cardRepo,
            ILoyaltyCardService cardSvc,
            IWalletCredentialService credSvc,
            IGoogleWalletServiceFactory googleFactory,
            IAppleWalletService appleService,
            IUnitOfWork uow,
            IAppEnvironment env
,
            ITenantBrandingRepository tenantBrandingRepository,
            IMapper mapper
,
UserManager<ApplicationUser> userManager,
            ICardNumberGenerator cardNumberGenerator,
            ILogger<CustomerEnrollmentService> logger)
        {
            _tp = tp; _customerRepo = customerRepo; _programRepo = programRepo;
            _cardRepo = cardRepo; _cardSvc = cardSvc; _credSvc = credSvc;
            _googleFactory = googleFactory; _appleService = appleService;
            _uow = uow; _env = env;
            _tenantBrandingRepository = tenantBrandingRepository;
            _mapper = mapper;
            _userManager = userManager;
            _cardNumberGenerator = cardNumberGenerator;
            _logger = logger;
        }



        // ——— Helpers ———
        private static string BuildTenantBaseUrl(string? subdomain)
        {
            // Lokal mühit üçün 7gul.localhost:7199 kimi istifadə edirsinizsə, onu sabitləşdirin:
            // Prod üçün isə məsələn: $"https://{subdomain}.domain.com"
            // Aşağıda lokala uyğun nümunə:
            
            // PORT-u ehtiyacınıza görə dəyişin:
            return $"https://walvero.com";
        }

        public async Task<ServiceResult> GetTenantCustomers(int tenantId, string username, CancellationToken ct = default)
        {
            var partners = await _customerRepo.GetByTenant(tenantId,1);
            if (partners != null)
            {
                var response = _mapper.Map<ICollection<GetCustomerDto>>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetByIdName(int customer, string username)
        {
            var partners = await _customerRepo.GetByIdAsync(customer);
            if (partners != null)
            {
                var response = _mapper.Map<GetCustomerDto>(partners);
                return new ServiceResult(true, response);   
            }

            return new ServiceResult(false);
        }
        public async Task<ServiceResult> StartEnrollmentAsync(AddCustomerWithCardDto dto, CancellationToken ct = default)
        {
            var tenantId = dto.TenantId;
            if (tenantId <= 0)
                return new ServiceResult(false, "Tenant not resolved.");

            var userId = _tp.CurrentUserId ?? "";
            if (string.IsNullOrWhiteSpace(userId))
                return new ServiceResult(false, "User not resolved.");

            // 1) Program tenant-ə məxsusdurmu?
            var program = await _programRepo.GetByIdNameAsync(dto.LoyaltyProgramId, ct);
            if (program is null || program.TenantId != tenantId)
                return new ServiceResult(false, "Program not found for this tenant.");

            // 2) User və Customer tap / yarat
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return new ServiceResult(false, "User not found.");
            }

            var email = user.Email?.Trim();
            var phone = user.UserName?.Trim();
            Customer? customer = null;

            if (!string.IsNullOrWhiteSpace(phone))
                customer = await _customerRepo.GetByPhoneAsync(tenantId, phone, ct);
           
            if (customer is null)
            {
                // Generate CardNumber before creating customer
                var cardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);

                customer = new Customer
                {
                    TenantId = tenantId,
                    FirstName = user.Name?.Trim() ?? "",
                    LastName = user.Surname?.Trim() ?? "",
                    PhoneNumber = phone ?? "",
                    Email = email,
                    Gender = user.Gender,
                    DateOfBirth = user.DateOfBirth,
                    IsActive = true,
                    LastActivityDate = DateTime.UtcNow,
                    applicationUserId = int.Parse(userId),
                    CardNumber = cardNumber // Set before save
                };

                await _uow.Repository<Customer>().AddAsync(customer);
                
                // Retry on unique constraint violation
                try
                {
                    _uow.Commit();
                }
                catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                {
                    // Regenerate and retry once
                    customer.CardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);
                    _uow.Commit();
                }
            }
            customer.applicationUserId = int.Parse(userId);
            // 3) Son aktiv kartı reuse et / yoxdursa deactive kartı aktivləşdir / yoxdursa yarat
            var card = await _cardRepo.GetLastActiveByCustomerAsync(tenantId, program.Id, customer.Id, ct);
            var hasPendingCardUpdate = false;
            var platformUpdated = false;
           
            // Əgər aktiv kart yoxdursa, deactive edilmiş kartı tap
            if (card is null)
            {
                var deactiveCard = await _cardRepo.GetLastInactiveByCustomerAsync(
                    tenantId,
                    program.Id,
                    customer.Id,
                    ct);
                
                if (deactiveCard != null)
                {
                    // Deactive edilmiş kartı aktivləşdir
                    deactiveCard.IsActive = true;
                    deactiveCard.Status = CardStatus.Active;
                    deactiveCard.ActivatedAt = DateTime.UtcNow;
                    deactiveCard.EditDate = DateTime.UtcNow;
                  
                    deactiveCard.EditUserId = int.Parse(userId);

                    _uow.Repository<LoyaltyCard>().Update(deactiveCard);
                    hasPendingCardUpdate = true;
                    card = deactiveCard;
                }

                if (card is null)
                {
                    
                    
                    card = new LoyaltyCard
                    {
                        TenantId = tenantId,
                        CustomerId = customer.Id,
                        LoyaltyProgramId = program.Id,
                        // CardNumber və BarcodeValue boş buraxılır 
                        BarcodeType = BarcodeType.Code128,
                        Status = CardStatus.Active,
                        ActivatedAt = DateTime.UtcNow,
                        RegUserId = int.Parse(userId),
                        RegDate = DateTime.UtcNow,
                        IsActive = true
                    };

                    await _uow.Repository<LoyaltyCard>().AddAsync(card);
                    _uow.Commit(); // Bu SaveChangesAsync çağırmalıdır - BarcodeValue avtomatik set olunacaq
                    hasPendingCardUpdate = false;
                }
            }
            else
            {
                // Aktiv kart varsa, onu yenilə (ActivatedAt və s.)
                if (card.ActivatedAt == null)
                {
                    card.ActivatedAt = DateTime.UtcNow;
                }
                card.EditDate = DateTime.UtcNow;
                card.EditUserId = int.Parse(userId);
                _uow.Repository<LoyaltyCard>().Update(card);
                hasPendingCardUpdate = true;
            }

            // 4) Program / branding / snapshot (VerifyEnrollment-dan götürdük)
            var branding = await _tenantBrandingRepository.GetByTenantIdAsync(tenantId);

            string baseUrl = "https://walvero.com";
            string ToAbs(string? dbPath)
            {
                if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                if (dbPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                    dbPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return dbPath;
                
                var normalizedBaseUrl = baseUrl.TrimEnd('/', '\\');
                var normalizedPath = dbPath.TrimStart('~').TrimStart('/', '\\');
                normalizedPath = string.IsNullOrWhiteSpace(normalizedPath) ? "/" : "/" + normalizedPath;
                return normalizedBaseUrl + normalizedPath;
            }

            var snapshot = CardTemplateSnapshotFactory.From(
                program,
                program.CardTemplate,
                branding,
                ToAbs
            );

            string? googleUrl = null;
            string? appleUrl = null;

            // 5) Platforma görə Google / Apple pass yarad
            if (string.Equals(dto.DevicePlatform, "android", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var gw = await _googleFactory.CreateForTenantAsync(tenantId, program.Id, ct);

                    await gw.EnsureClassAsync(snapshot, program.ProgramName, ct);

                    await gw.CreateOrGetObjectByCardIdAsync(
                        loyaltyCardId: card.Id,
                        tpl: snapshot,
                        accountName: $"{customer.FirstName} {customer.LastName}".Trim(),
                        barcodeValue: card.BarcodeValue,
                        currentPoints: card.CurrentPoints,
                        ct: ct
                    );

                    var save = await gw.GenerateSaveLinkByCardIdAsync(card.Id, ct);
                    if (save.Success)
                        googleUrl = save.Data as string ?? save.Message as string;

                    card.IsAndroid = true;
                    platformUpdated = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Google Wallet credentials missing or error occurred during enrollment. TenantId={TenantId}, ProgramId={ProgramId}, CardId={CardId}, CustomerId={CustomerId}", 
                        tenantId, program.Id, card.Id, customer.Id);
                    // Continue without Google wallet URL - do not set IsAndroid flag
                }
            }
            else if (string.Equals(dto.DevicePlatform, "ios", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var acred = await _credSvc.GetByProgram(tenantId, WalletPlatform.AppleWallet);
                    if (acred?.Success == true && acred.Data is GetWalletCredentialDto c)
                    {
                        var appleCreds = new AppleWalletRuntimeCreds
                        {
                            TeamIdentifier = c.TeamIdentifier!,
                            PassTypeIdentifier = c.PassTypeIdentifier!,
                            CertP12Path = ToAbs(c.AppleCertificateUrl),
                            CertPassword = c.CertificatePassword,
                            WwdrCertPath = ToAbs(c.AppleWwdrCertUrl),
                            WebServiceUrl = c.PassKitServiceUrl,
                            AuthToken = c.PassKitAuthToken
                        };

                        var appleRes = await _appleService.GeneratePassUrlByCardIdAsync(
                            loyaltyCardId: card.Id,
                            creds: appleCreds,
                            snapshot: snapshot,
                            customerFullName: $"{customer.FirstName} {customer.LastName}".Trim(),
                            barcodeValue: card.BarcodeValue ?? card.CardNumber,
                            ct: ct
                        );

                        if (appleRes.Success)
                            appleUrl = appleRes.Data as string ?? appleRes.Message as string;
                    }
                    else
                    {
                        _logger.LogWarning("Apple Wallet credentials not found. TenantId={TenantId}, ProgramId={ProgramId}, CardId={CardId}, CustomerId={CustomerId}", 
                            tenantId, program.Id, card.Id, customer.Id);
                    }

                    card.IsIOS = true;
                    platformUpdated = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Apple Wallet credentials missing or error occurred during enrollment. TenantId={TenantId}, ProgramId={ProgramId}, CardId={CardId}, CustomerId={CustomerId}", 
                        tenantId, program.Id, card.Id, customer.Id);
                    // Continue without Apple wallet URL - do not set IsIOS flag
                }
            }

            var needsFinalUpdate = hasPendingCardUpdate || platformUpdated;

            if (needsFinalUpdate)
            {
                _uow.Repository<LoyaltyCard>().Update(card);
                _uow.Commit();
            }

            // 6) Nəticə – artıq OTP yox, birbaşa wallet linkləri qaytarırıq
            return new ServiceResult(true, new VerifyEnrollmentResultDto
            {
                GoogleSaveUrl = googleUrl,
                ApplePassUrl = appleUrl,
                CardId = card.Id
                // istəsən buraya CustomerId, ProgramId də əlavə edə bilərsən DTO-nu genişləndirib
            });
        }


        public async Task<ServiceResult> VerifyEnrollmentAsync(VerifyCustomerOtpDto dto, CancellationToken ct = default)
        {
            var customer = await _uow.Repository<Customer>().GetByIdAsync(dto.CustomerId);
            if (customer is null) return new ServiceResult(false, "Customer not found.");

            if (string.IsNullOrWhiteSpace(customer.OtpHash) || string.IsNullOrWhiteSpace(customer.OtpSalt) || !customer.OtpExpiresAt.HasValue)
                return new ServiceResult(false, "No active code.");

            if (DateTime.UtcNow > customer.OtpExpiresAt.Value)
                return new ServiceResult(false, "Code expired.");

            if (customer.OtpAttempts >= 5)
                return new ServiceResult(false, "Too many attempts.");

            // yoxla
            customer.OtpAttempts++;
            var ok =WalletCodeHelper.HashOtp(dto.Code ?? "", customer.OtpSalt) == customer.OtpHash;
            if (!ok)
            {
                _uow.Repository<Customer>().Update(customer);
                _uow.Commit();
                return new ServiceResult(false, "Invalid code.");
            }

            // uğurlu – OTP sahələrini sıfırla
            customer.OtpHash = null;
            customer.OtpSalt = null;
            customer.OtpExpiresAt = null;
            customer.OtpAttempts = 0;
            _uow.Repository<Customer>().Update(customer);
            _uow.Commit();

            // Müştərinin son aktiv kartını götür
            // (və ya frontdan CardId ötürürsə, onu istifadə et)
            var tenantId = _tp.CurrentTenantId ?? 1;
            var programId =dto.LoyaltyProgramId;
            var card = await _cardRepo.GetLastActiveByCustomerAsync(tenantId, programId, customer.Id, ct);
            if (card is null) return new ServiceResult(false, "Card not found.");

            // Program/snapshot
            var program = await _programRepo.GetByIdNameAsync(card.LoyaltyProgramId, ct);
            if (program is null) return new ServiceResult(false, "Program missing.");

            var branding = await _tenantBrandingRepository.GetByTenantIdAsync(tenantId);
            string baseUrl ="https://walvero.com";
            string ToAbs(string? dbPath)
            {
                if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                if (dbPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return dbPath;
                var rel = "/" + dbPath.TrimStart('/', '\\');
                return baseUrl + rel;
            }
            var snapshot = CardTemplateSnapshotFactory.From(program, program.CardTemplate, branding, ToAbs);

            // Google save link
            string? googleUrl = null, appleUrl = null;
            if(dto.DevicePlatform == "android")
            {
                try
                {
                    var gw = await _googleFactory.CreateForTenantAsync(tenantId, program.Id, ct);
                    await gw.EnsureClassAsync(snapshot, program.ProgramName, ct);
                    await gw.CreateOrGetObjectByCardIdAsync(
                        loyaltyCardId: card.Id,
                        tpl: snapshot,
                        accountName: $"{customer.FirstName} {customer.LastName}".Trim(),
                        barcodeValue: card.BarcodeValue,
                        currentPoints:card.CurrentPoints,
                        ct: ct
                    );
                    var save = await gw.GenerateSaveLinkByCardIdAsync(card.Id, ct);
                    if (save.Success) googleUrl = (string?)save.Data ?? save.Message as string;
                    card.IsAndroid = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Google Wallet credentials missing or error occurred during verification. TenantId={TenantId}, ProgramId={ProgramId}, CardId={CardId}, CustomerId={CustomerId}", 
                        tenantId, program.Id, card.Id, customer.Id);
                    // Continue without Google wallet URL - do not set IsAndroid flag
                }
            }
            else if(dto.DevicePlatform == "ios")
            {
                try
                {
                    var acred = await _credSvc.GetByProgram(tenantId, WalletPlatform.AppleWallet);
                    if (acred?.Success == true && acred.Data is GetWalletCredentialDto c)
                    {
                        // WWDR / P12 yollarını absolute et


                        var appleCreds = new AppleWalletRuntimeCreds
                        {
                            TeamIdentifier = c.TeamIdentifier!,          // DB
                            PassTypeIdentifier = c.PassTypeIdentifier!,      // DB
                            CertP12Path = ToAbs(c.AppleCertificateUrl),        // DB-də saxladığın nisbi yol
                            CertPassword = c.CertificatePassword,      // DB
                            WwdrCertPath = ToAbs(c.AppleWwdrCertUrl),   // DB (WWDR .cer)
                            WebServiceUrl = c.PassKitServiceUrl,        // DB (opsional – push update üçün)
                            AuthToken = c.PassKitAuthToken          // DB (opsional)
                        };

                        // Apple pass yarat/yaxud mövcudu reuse et → URL
                        var appleRes = await _appleService.GeneratePassUrlByCardIdAsync(
                             loyaltyCardId: card.Id,
                             creds: appleCreds,
                             snapshot: snapshot,
                             customerFullName: $"{customer.FirstName} {customer.LastName}".Trim(),
                             barcodeValue: card.BarcodeValue ?? card.CardNumber,
                             ct: ct
                         );

                        if (appleRes.Success)
                            appleUrl = appleRes.Data as string ?? appleRes.Message as string;
                    }
                    else
                    {
                        _logger.LogWarning("Apple Wallet credentials not found. TenantId={TenantId}, ProgramId={ProgramId}, CardId={CardId}, CustomerId={CustomerId}", 
                            tenantId, program.Id, card.Id, customer.Id);
                    }
                    card.IsIOS = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Apple Wallet credentials missing or error occurred during verification. TenantId={TenantId}, ProgramId={ProgramId}, CardId={CardId}, CustomerId={CustomerId}", 
                        tenantId, program.Id, card.Id, customer.Id);
                    // Continue without Apple wallet URL - do not set IsIOS flag
                }
            }
            _uow.Repository<LoyaltyCard>().Update(card);
            _uow.Commit();
            // Apple (opsional)



            return new ServiceResult(true, new VerifyEnrollmentResultDto
            {
                GoogleSaveUrl = googleUrl,
                ApplePassUrl = appleUrl,
                CardId = card.Id
            });
        }

        public async Task<ServiceResult> GetByUserId(int userId)
        {
            var partners = await _customerRepo.GetByUserId(userId);
            if (partners != null)
            {
                var response = _mapper.Map<ICollection<GetCustomerDto>>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }
    }

}
