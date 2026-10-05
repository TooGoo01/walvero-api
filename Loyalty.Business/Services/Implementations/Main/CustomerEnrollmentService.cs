using AutoMapper;
using Loyalty.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Azure.Core;
using Google.Apis.Walletobjects.v1.Data;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Resources;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Sequences;
using Loyalty.Business.Services.Abstractions.Users;
using Microsoft.EntityFrameworkCore;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Implementations.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly ICustomerSyncService _customerSyncService;
        private readonly IPosSyncService _posSyncService;
        private readonly ILogger<CustomerEnrollmentService> _logger;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly ITenantStatusGuard _tenantStatusGuard;
        private readonly ITenantSuspensionGuard _tenantSuspensionGuard;
        // POS sync-i ayrı DI scope-da işlətmək üçün — enrollment DbContext-ini
        // zəhərləməsin (iiko multi-venue sync uğursuz olsa da kart əlavəsi keçir).
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IWelcomeBonusService _welcomeBonus;
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
            ICustomerSyncService customerSyncService,
            IPosSyncService posSyncService,
            ILogger<CustomerEnrollmentService> logger,
            LoyaltyWalletDbContext dbContext,
            ITenantStatusGuard tenantStatusGuard,
            ITenantSuspensionGuard tenantSuspensionGuard,
            IServiceScopeFactory scopeFactory,
            IWelcomeBonusService welcomeBonus)
        {
            _tp = tp; _customerRepo = customerRepo; _programRepo = programRepo;
            _cardRepo = cardRepo; _cardSvc = cardSvc; _credSvc = credSvc;
            _googleFactory = googleFactory; _appleService = appleService;
            _uow = uow; _env = env;
            _tenantBrandingRepository = tenantBrandingRepository;
            _mapper = mapper;
            _userManager = userManager;
            _cardNumberGenerator = cardNumberGenerator;
            _customerSyncService = customerSyncService;
            _posSyncService = posSyncService;
            _logger = logger;
            _dbContext = dbContext;
            _tenantStatusGuard = tenantStatusGuard;
            _tenantSuspensionGuard = tenantSuspensionGuard;
            _scopeFactory = scopeFactory;
            _welcomeBonus = welcomeBonus;
        }



        // ——— Helpers ———
        private static string BuildTenantBaseUrl(string? subdomain)
        {
            return $"https://appapi.walvero.com";
        }

        /// <summary>
        /// POS customer sync-i AYRI DI scope-da (öz DbContext-i ilə) işlədir.
        /// Best-effort: iiko multi-venue sync uğursuz olsa belə enrollment
        /// context-i zəhərlənmir — kart əlavəsi/OTP təsdiqi 500 vermir.
        /// </summary>
        private async Task SyncPosSafeAsync(int tenantId, int customerId, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IPosSyncService>();
                var db = scope.ServiceProvider.GetRequiredService<LoyaltyWalletDbContext>();
                var customer = await db.Set<Customer>().FirstOrDefaultAsync(x => x.Id == customerId, ct);
                if (customer == null) return;
                await sync.SyncCustomerToPosAsync(tenantId, customer, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "POS sync (isolated) failed for CustomerId={CustomerId}, TenantId={TenantId}", customerId, tenantId);
            }
        }

        public async Task<ServiceResult> GetTenantCustomers(int tenantId, string username, CancellationToken ct = default)
        {
            var partners = await _customerRepo.GetByTenant(tenantId,1);
            if (partners != null)
            {
                var response = _mapper.Map<ICollection<GetCustomerListItemDto>>(partners);
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
                return new ServiceResult(false, AppMessages.Get("TenantNotResolved"));

            var userId = _tp.CurrentUserId ?? "";
            if (string.IsNullOrWhiteSpace(userId))
                return new ServiceResult(false, "User not resolved.");

            // 1) Program tenant-ə məxsusdurmu?
            var program = await _programRepo.GetByIdNameAsync(dto.LoyaltyProgramId, ct);
            if (program is null || program.TenantId != tenantId)
                return new ServiceResult(false, "Program not found for this tenant.");

            decimal signupGift = 0m;

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

            // Nömrə dəyişibsə, əvvəlki customer-i applicationUserId ilə tap
            if (customer is null)
            {
                var appUserId = int.Parse(userId);
                var existingCustomers = await _customerRepo.GetByUserId(appUserId);
                customer = existingCustomers.FirstOrDefault(c => c.TenantId == tenantId && c.IsActive);

                // Köhnə customer tapıldısa, nömrəni yenilə
                if (customer != null && !string.IsNullOrWhiteSpace(phone))
                {
                    customer.PhoneNumber = phone;
                    customer.LastActivityDate = DateTime.UtcNow;
                    _uow.Repository<Customer>().Update(customer);
                    _uow.Commit();
                }
            }

            // Reactivate if customer exists but is inactive
            if (customer != null && !customer.IsActive)
            {
                customer.IsActive = true;
                customer.LastActivityDate = DateTime.UtcNow;
                _uow.Repository<Customer>().Update(customer);
                _uow.Commit();
            }

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

                // Sync new customer to POS systems (ayrı scope — context zəhərlənməsin)
                await SyncPosSafeAsync(tenantId, customer.Id, ct);
            }
            customer.applicationUserId = int.Parse(userId);
            // 3) Son aktiv kartı reuse et / yoxdursa deactive kartı aktivləşdir / yoxdursa yarat
            var card = await _cardRepo.GetLastActiveByCustomerAsync(tenantId, program.Id, customer.Id, ct);
            var hasPendingCardUpdate = false;
           
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

                    signupGift = await _welcomeBonus.TryGrantSignupGiftAsync(card, program,
                        program.CardTemplate?.EffectiveType ?? CardTemplateType.PointsBased,
                        program.CardTemplate?.TotalSteps ?? 10, ct);
                    if (signupGift > 0) _uow.Commit();
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

            // Auto-sync customer to POS systems (ayrı scope — context zəhərlənməsin)
            await SyncPosSafeAsync(tenantId, customer.Id, ct);
            if (signupGift > 0)
                await _welcomeBonus.SyncPosBalanceSafeAsync(tenantId, customer.Id, card.CurrentPoints, ct);

            // 4) Son update — kart dəyişiklikləri varsa commit et
            if (hasPendingCardUpdate)
            {
                _uow.Repository<LoyaltyCard>().Update(card);
                _uow.Commit();
            }

            // 5) Nəticə — yalnız customer + card yaradıldı, wallet pass sonra ayrıca yaradılacaq
            return new ServiceResult(true, new VerifyEnrollmentResultDto
            {
                CardId = card.Id
            });
        }


        /// <summary>
        /// Public enrollment – auth tələb etmir, DTO-dan personal info alır, OTP göndərir.
        /// Webdəki Add.cshtml flow-u ilə eynidir.
        /// </summary>
        public async Task<ServiceResult> StartPublicEnrollmentAsync(PublicEnrollmentStartDto dto, CancellationToken ct = default)
        {
            var tenantId = dto.TenantId;
            if (tenantId <= 0)
                return new ServiceResult(false, AppMessages.Get("TenantNotResolved"));

            // Deaktiv tenant-a yeni müştəri qoşulması qadağandır (abunə dayandırılıb)
            if (!await _tenantStatusGuard.IsActiveAsync(tenantId, ct))
                return new ServiceResult(false, TenantStatusGuard.InactiveMessage);

            if (string.IsNullOrWhiteSpace(dto.FirstName))
                return new ServiceResult(false, AppMessages.Get("NameRequired"));
            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                return new ServiceResult(false, AppMessages.Get("PhoneRequired"));

            // 1) Program tenant-ə məxsusdurmu?
            var program = await _programRepo.GetByIdNameAsync(dto.LoyaltyProgramId, ct);
            if (program is null || program.TenantId != tenantId)
                return new ServiceResult(false, "Program not found for this tenant.");

            var phone = dto.PhoneNumber.Trim();
            var email = dto.Email?.Trim();

            // 2) Customer tap / yarat
            Customer? customer = await _customerRepo.GetByPhoneAsync(tenantId, phone, ct);

            // Reactivate if customer exists but is inactive
            if (customer != null && !customer.IsActive)
            {
                customer.IsActive = true;
                customer.LastActivityDate = DateTime.UtcNow;
                _uow.Repository<Customer>().Update(customer);
                _uow.Commit();
            }

            if (customer is null)
            {
                var cardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);

                // Phone ilə mövcud ApplicationUser axtar (mobile app istifadəçisi)
                // Telefon formatı fərqi ola bilər (+994, 994, 050) - normallaşdıraraq axtarış et
                var phoneDigits = new string(phone.Where(char.IsDigit).ToArray());
                var existingAppUser = await _userManager.Users
                    .FirstOrDefaultAsync(u =>
                        u.PhoneNumber == phone ||
                        u.PhoneNumber == phoneDigits ||
                        u.PhoneNumber == "+" + phoneDigits, ct);

                customer = new Customer
                {
                    TenantId = tenantId,
                    FirstName = dto.FirstName.Trim(),
                    LastName = dto.LastName?.Trim() ?? "",
                    PhoneNumber = phone,
                    Email = email,
                    Gender = dto.Gender.HasValue ? (Gender)dto.Gender.Value : null,
                    DateOfBirth = dto.DateOfBirth,
                    IsActive = true,
                    LastActivityDate = DateTime.UtcNow,
                    CardNumber = cardNumber,
                    applicationUserId = existingAppUser?.Id
                };

                await _uow.Repository<Customer>().AddAsync(customer);
                try
                {
                    _uow.Commit();
                }
                catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                {
                    customer.CardNumber = await _cardNumberGenerator.GenerateCardNumberAsync(tenantId, ct);
                    _uow.Commit();
                }
            }
            else
            {
                // Mövcud customer – məlumatları yenilə
                customer.FirstName = dto.FirstName.Trim();
                customer.LastName = dto.LastName?.Trim() ?? customer.LastName;
                customer.Email = email;
                customer.Gender = dto.Gender.HasValue ? (Gender)dto.Gender.Value : customer.Gender;
                customer.DateOfBirth = dto.DateOfBirth ?? customer.DateOfBirth;
                customer.LastActivityDate = DateTime.UtcNow;

                // Əgər hələ ApplicationUser ilə linkli deyilsə, phone ilə axtar və bağla
                if (!customer.applicationUserId.HasValue)
                {
                    var phoneDigits2 = new string(phone.Where(char.IsDigit).ToArray());
                    var existingAppUser = await _userManager.Users
                        .FirstOrDefaultAsync(u =>
                            u.PhoneNumber == phone ||
                            u.PhoneNumber == phoneDigits2 ||
                            u.PhoneNumber == "+" + phoneDigits2, ct);
                    if (existingAppUser != null)
                        customer.applicationUserId = existingAppUser.Id;
                }
            }

            // 3) Kart tap / yarat
            var card = await _cardRepo.GetLastActiveByCustomerAsync(tenantId, program.Id, customer.Id, ct);
            if (card is null)
            {
                var deactiveCard = await _cardRepo.GetLastInactiveByCustomerAsync(tenantId, program.Id, customer.Id, ct);
                if (deactiveCard != null)
                {
                    deactiveCard.IsActive = true;
                    deactiveCard.Status = CardStatus.Active;
                    deactiveCard.ActivatedAt = DateTime.UtcNow;
                    deactiveCard.EditDate = DateTime.UtcNow;
                    _uow.Repository<LoyaltyCard>().Update(deactiveCard);
                    card = deactiveCard;
                }
                else
                {
                    card = new LoyaltyCard
                    {
                        TenantId = tenantId,
                        CustomerId = customer.Id,
                        LoyaltyProgramId = program.Id,
                        BarcodeType = BarcodeType.Code128,
                        Status = CardStatus.Active,
                        ActivatedAt = DateTime.UtcNow,
                        RegDate = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _uow.Repository<LoyaltyCard>().AddAsync(card);
                    _uow.Commit();
                }
            }

            // 4) OTP throttle yoxla (30 saniyə)
            if (customer.OtpLastSentAt.HasValue &&
                (DateTime.UtcNow - customer.OtpLastSentAt.Value).TotalSeconds < 30)
            {
                return new ServiceResult(false, AppMessages.Get("Wait30Sec"));
            }

            // 5) OTP yarat, hash-la, saxla
            var otp = WalletCodeHelper.NewOtp(6);
            var salt = WalletCodeHelper.NewSalt();
            var hash = WalletCodeHelper.HashOtp(otp, salt);

            customer.OtpSalt = salt;
            customer.OtpHash = hash;
            customer.OtpExpiresAt = DateTime.UtcNow.AddMinutes(5);
            customer.OtpAttempts = 0;
            customer.OtpLastSentAt = DateTime.UtcNow;
            customer.OtpChannel = "sms";
            customer.OtpDestinationMasked = WalletCodeHelper.MaskPhone(phone);

            _uow.Repository<Customer>().Update(customer);
            _uow.Commit();

            // 6) SMS göndər
            try
            {
                var smsText = $"Sizin OTP: {otp}\nwalvero.com #{otp}";
                await Helper.SendSmsAsync(phone, smsText, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMS göndərilə bilmədi. CustomerId={CustomerId}, Phone={Phone}", customer.Id, phone);
            }

            // 7) Nəticə
            return new ServiceResult(true, new StartEnrollmentResultDto
            {
                CustomerId = customer.Id,
                MaskedDestination = customer.OtpDestinationMasked,
                CardId = card.Id,
                ProgramId = program.Id
            });
        }

        public async Task<ServiceResult> VerifyEnrollmentAsync(VerifyCustomerOtpDto dto, CancellationToken ct = default)
        {
            var customer = await _uow.Repository<Customer>().GetByIdAsync(dto.CustomerId);
            if (customer is null) return new ServiceResult(false, AppMessages.Get("CustomerNotFound"));

            // Deaktiv tenant-da qoşulma təsdiqi də qadağandır
            if (!await _tenantStatusGuard.IsActiveAsync(customer.TenantId, ct))
                return new ServiceResult(false, TenantStatusGuard.InactiveMessage);

            if (string.IsNullOrWhiteSpace(customer.OtpHash) || string.IsNullOrWhiteSpace(customer.OtpSalt) || !customer.OtpExpiresAt.HasValue)
                return new ServiceResult(false, AppMessages.Get("CodeNotFound"));

            if (DateTime.UtcNow > customer.OtpExpiresAt.Value)
                return new ServiceResult(false, AppMessages.Get("CodeExpired"));

            if (customer.OtpAttempts >= 5)
                return new ServiceResult(false, "Too many attempts.");

            // yoxla
            customer.OtpAttempts++;
            var ok =WalletCodeHelper.HashOtp(dto.Code ?? "", customer.OtpSalt) == customer.OtpHash;
            if (!ok)
            {
                _uow.Repository<Customer>().Update(customer);
                _uow.Commit();
                return new ServiceResult(false, AppMessages.Get("CodeInvalid"));
            }

            // uğurlu – OTP sahələrini sıfırla
            customer.OtpHash = null;
            customer.OtpSalt = null;
            customer.OtpExpiresAt = null;
            customer.OtpAttempts = 0;
            _uow.Repository<Customer>().Update(customer);
            _uow.Commit();

            // Auto-sync customer to POS systems (ayrı scope — context zəhərlənməsin)
            await SyncPosSafeAsync(customer.TenantId, customer.Id, ct);

            // Müştərinin son aktiv kartını götür
            // (və ya frontdan CardId ötürürsə, onu istifadə et)
            var tenantId = customer.TenantId;
            var programId = dto.LoyaltyProgramId;
            var card = await _cardRepo.GetLastActiveByCustomerAsync(tenantId, programId, customer.Id, ct);
            if (card is null) return new ServiceResult(false, "Card not found.");

            // Program/snapshot (CardTemplate include ilə)
            var program = await _dbContext.Set<LoyaltyProgram>()
                .Include(p => p.CardTemplate)
                    .ThenInclude(ct2 => ct2.Icons)
                .FirstOrDefaultAsync(p => p.Id == card.LoyaltyProgramId, ct);
            if (program is null) return new ServiceResult(false, "Program missing.");
            if (program.CardTemplate is null) return new ServiceResult(false, "Card template missing.");

            var branding = await _tenantBrandingRepository.GetByTenantIdAsync(tenantId);
            string baseUrl = "https://appapi.walvero.com";
            string ToAbs(string? dbPath)
            {
                if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                if (dbPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return dbPath;
                var rel = "/" + dbPath.TrimStart('/', '\\');
                return baseUrl + rel;
            }
            var snapshot = CardTemplateSnapshotFactory.From(program, program.CardTemplate, branding, ToAbs);

            // Hədiyyə pass yaranmazdan əvvəl ki, balans pass-a düşsün
            var signupGift = await _welcomeBonus.TryGrantSignupGiftAsync(card, program, snapshot.TemplateType, snapshot.TotalSteps, ct);
            if (signupGift > 0) _uow.Commit();

            // Tenant company name for wallet header (separate query — avoids cycle issues from Include).
            snapshot.TenantCompanyName = await _dbContext.Set<Tenant>()
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.CompanyName)
                .FirstOrDefaultAsync(ct);

            // Suspension mask — suspend olunmuş tenant-da enrollment zamanı da pass masklı yaranır.
            // (Apple tərəfi AppleWalletService.GeneratePassUrlByCardIdAsync içində mərkəzi maskalanır.)
            var suspension = await _tenantSuspensionGuard.GetAsync(tenantId, ct);
            var walletAccountName = suspension.IsSuspended
                ? TenantSuspensionMask.EffectiveMessage(suspension.Message)
                : $"{customer.FirstName} {customer.LastName}".Trim();

            // Google save link
            string? googleUrl = null, appleUrl = null;
            if(dto.DevicePlatform == "android")
            {
                try
                {
                    var gw = await _googleFactory.CreateForTenantAsync(tenantId, program.Id, ct);
                    var issuerName = snapshot.TenantCompanyName ?? program.ProgramName;
                    await gw.EnsureClassAsync(snapshot, issuerName, ct);
                    await gw.CreateOrGetObjectByCardIdAsync(
                        loyaltyCardId: card.Id,
                        tpl: snapshot,
                        accountName: walletAccountName,
                        barcodeValue: card.BarcodeValue,
                        currentPoints: suspension.IsSuspended ? 0 : card.CurrentPoints,
                        ct: ct,
                        lotteryTickets: 0,
                        completedCyclesOverride: suspension.IsSuspended ? 0 : (int?)null
                    );
                    var save = await gw.GenerateSaveLinkByCardIdAsync(card.Id, tenantId, program.Id, ct);
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

            // Diaqnostika: pass Wallet-ə düşməyən hallarda brauzer kontekstini görmək üçün
            // (in-app brauzer / Chrome iOS .pkpass-ı Wallet-ə ötürmür). App Insights traces-ə düşür.
            _logger.LogInformation(
                "Enrollment verify: TenantId={TenantId} CardId={CardId} Platform={Platform} AppleUrl={HasAppleUrl} GoogleUrl={HasGoogleUrl} UserAgent={UserAgent}",
                tenantId, card.Id, dto.DevicePlatform, appleUrl != null, googleUrl != null, dto.UserAgent);

            if (signupGift > 0)
                await _welcomeBonus.SyncPosBalanceSafeAsync(tenantId, customer.Id, card.CurrentPoints, ct);
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
