using AutoMapper;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Mappings; // Profil(lər)inizin namespace-i (məs: TenantMappingProfile)
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Sequences;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.Business.Services.Abstractions.Storage.Azure;
using Loyalty.Business.Services.Abstractions.Users;
using Loyalty.Business.Services.Implementations;
using Loyalty.Business.Services.Implementations.Main;
using Loyalty.Business.Services.Implementations.Sequences;
using Loyalty.Business.Services.Implementations.Storage;
using Loyalty.Business.Services.Implementations.Users;
using Loyalty.DataAccess.Entities.Main;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loyalty.Business.Services
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddBusinessServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // 1) AutoMapper-i yüklə — profil tiplərini konkret göstər
            services.AddAutoMapper(cfg =>
            {
                cfg.ShouldMapProperty = p => p.GetMethod.IsPublic || p.GetMethod.IsAssembly;
                cfg.AddProfile<MapProfile>();
            });

            // 2) appsettings: "GoogleWallet" bölməsini options-a bağla (əgər hələ istifadə edirsinizsə)
            services.Configure<GoogleWalletOptions>(configuration.GetSection("GoogleWallet"));

            // 3) Servis lifetimeləri (Scoped)
            services.AddSingleton<Loyalty.Business.Services.Implementations.Storage.BlobUrlRewriter>();
            services.AddScoped<IAzureStorage, AzureStorage>();
            services.AddScoped<IContactService, ContactService>();
            services.AddScoped<ITenantService, TenantService>();
            services.AddScoped<ITenantBrandingService, TenantBrandingService>();
            services.AddScoped<IStorage, AzureStorage>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IOtpService, OtpService>();
            services.AddScoped<IMailService, MailService>();
            services.AddScoped<IWalletCredentialService, WalletCredentialService>();
            services.AddScoped<ICardTemplateService, CardTemplateService>();
            services.AddScoped<ICardTemplateIconService, CardTemplateIconService>();
            services.AddScoped<ICustomerEnrollmentService, CustomerEnrollmentService>();
            services.AddScoped<ILoyaltyCardService, LoyaltyCardService>();
             services.AddScoped<GoogleWalletRedeemer>();
             services.AddScoped<AppleWalletRedeemer>();
            services.AddScoped<IWalletRouter, WalletRouter>();
            services.AddScoped<IRedeemService, RedeemService>();
            services.AddScoped<IReceiptValidationService, ReceiptValidationService>();
            services.AddScoped<IWhatsAppAlertService, TwilioWhatsAppAlertService>();
            services.AddScoped<IEkassaImageFetcher, EkassaImageFetcher>();
            services.AddHttpClient(EkassaImageFetcher.HttpClientName, c =>
            {
                c.Timeout = TimeSpan.FromSeconds(30);
                // Real browser UA — bəzi backend firewall-ları default .NET UA bloklayır.
                c.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                    "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                c.DefaultRequestHeaders.Accept.ParseAdd("image/jpeg,image/png,image/*,*/*;q=0.8");
                c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9,az;q=0.8");
                c.DefaultRequestHeaders.Referrer = new Uri("https://monitoring.e-kassa.gov.az/");
            });
            services.AddScoped<ReceiptOcrJob>();
            services.AddScoped<PosBatchOperationJob>();
            services.AddScoped<IOtpGenerator, OtpGenerator>();
            services.AddScoped<IWalletCredentialProvider, WalletCredentialProvider>();
            services.AddScoped<IAppleWalletFactory, AppleWalletFactory>();
            services.AddSingleton<FileDownloader>();
            services.AddSingleton<IApplePassBuilder, ApplePassBuilder>();
            services.AddSingleton<IApplePassSigner, ApplePassSigner>();
            services.AddScoped<IAppleWalletService, AppleWalletService>();
            services.AddScoped<ILoyaltyProgramService, LoyaltyProgramService>();
            services.AddScoped<IPasskitPushService, ApnsPasskitPushService>();
            services.AddScoped<IPasskitService, PasskitService>();
            services.AddScoped<ITransactionService, TransactionService>();
            services.AddScoped<IStatisticsService, StatisticsService>();
            services.AddScoped<IFilePathResolver, FilePathResolver>();
            services.AddScoped<IUserCardService, UserCardService>();
            services.AddScoped<IDeviceTokenService, DeviceTokenService>();
            services.AddScoped<INotificationService, FcmNotificationService>();
            services.AddScoped<IUserNotificationService, UserNotificationService>();
            services.AddScoped<IWalletMessagingService, WalletMessagingService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IAppVersionService, AppVersionService>();
            services.AddScoped<IFeedbackService, FeedbackService>();
            services.AddScoped<IGuestAuthService, GuestAuthService>();
            services.AddScoped<IOfferService, OfferService>();
            services.AddScoped<IStoryService, StoryService>();
            services.AddScoped<IHomeService, HomeService>();
            services.AddScoped<IWalletCardDetailsService, WalletCardDetailsService>();
            services.AddScoped<IWalveroWalletPassService, WalveroWalletPassService>();
            services.AddScoped<ICardNumberGenerator, CardNumberGenerator>();
            services.AddScoped<IWalveroIdGenerator, WalveroIdGenerator>();
            services.AddScoped<ICountrySequenceService, CountrySequenceService>();
            services.AddScoped<IPosSessionService, PosSessionService>();
            services.AddScoped<ITransactionReverseService, TransactionReverseService>();
            services.AddScoped<ICountryService, CountryService>();
            services.AddScoped<ICityService, CityService>();
            services.AddScoped<ITimeZoneService, TimeZoneService>();
            
            // Public tenant data service
            services.AddScoped<IPublicTenantDataService, PublicTenantDataService>();

            // Menu related services
            services.AddScoped<IMenuCategoryService, MenuCategoryService>();
            services.AddScoped<IMenuItemService, MenuItemService>();
            services.AddScoped<IMenuItemImageService, MenuItemImageService>();
            services.AddScoped<IRestorantWifiInformationService, RestorantWifiInformationService>();
            services.AddScoped<ITenantLinkService, TenantLinkService>();
            services.AddScoped<ITenantIntegrationResolver, TenantIntegrationResolver>();
            services.AddScoped<ITenantIntegrationService, TenantIntegrationService>();
            services.AddScoped<IPosProductEarnRuleService, PosProductEarnRuleService>();
            services.AddScoped<IPosLoyaltyDiscountTypeService, PosLoyaltyDiscountTypeService>();
            services.AddScoped<IRewardCrudService, RewardCrudService>();
            services.AddScoped<IWalletWebhookService, WalletWebhookService>();
            services.AddScoped<IMonyoPosApiClient, MonyoPosApiClient>();
            services.AddScoped<IIikoPosApiClient, IikoPosApiClient>();
            services.AddScoped<ICustomerSyncService, CustomerSyncService>();
            services.AddScoped<ILotteryService, LotteryService>();
            services.AddScoped<IBranchService, BranchService>();
            services.AddSingleton<IMediaProcessor, MediaProcessor>();

            services.AddScoped<ISalesRepVisitService, SalesRepVisitService>();
            services.AddScoped<IPosSyncService, PosSyncService>();
            services.AddScoped<IBranchService, BranchService>();
            services.AddScoped<IRoomService, RoomService>();

            services.AddSignalR(options =>
            {
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                options.MaximumReceiveMessageSize = 64 * 1024; // 64KB
            });
            return services;
        }
    }
}
