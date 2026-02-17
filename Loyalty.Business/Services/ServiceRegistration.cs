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
            services.AddScoped<IAzureStorage, AzureStorage>();
            services.AddScoped<IContactService, ContactService>();
            services.AddScoped<ITenantService, TenantService>();
            services.AddScoped<ITenantBrandingService, TenantBrandingService>();
            services.AddScoped<IStorage, FileStorage>();
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
            services.AddScoped<IFilePathResolver, FilePathResolver>();
            services.AddScoped<IUserCardService, UserCardService>();
            services.AddScoped<IDeviceTokenService, DeviceTokenService>();
            services.AddScoped<INotificationService, FcmNotificationService>();
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
            
            services.AddSignalR();
            return services;
        }
    }
}
