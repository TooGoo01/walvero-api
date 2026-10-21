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
using System.Net;
using System.Net.Sockets;

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
            services.AddScoped<ICardDesignPreviewService, CardDesignPreviewService>();
            services.AddScoped<ICardTemplateIconService, CardTemplateIconService>();
            services.AddScoped<ICustomerEnrollmentService, CustomerEnrollmentService>();
            services.AddScoped<IAmbassadorService, AmbassadorService>();
            services.AddScoped<ILoyaltyCardService, LoyaltyCardService>();
             services.AddScoped<GoogleWalletRedeemer>();
             services.AddScoped<AppleWalletRedeemer>();
            services.AddScoped<IWalletRouter, WalletRouter>();
            services.AddScoped<IRedeemService, RedeemService>();
            services.AddScoped<IReceiptValidationService, ReceiptValidationService>();
            services.AddScoped<IReceiptVisionOcr, ReceiptVisionOcrService>();
            services.AddScoped<IMenuPopupService, MenuPopupService>();
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
            services.AddScoped<ITelegramSettingsProvider, TelegramSettingsProvider>();
            services.AddScoped<ITelegramSender, TelegramSender>();
            services.AddScoped<ITelegramBotService, TelegramBotService>();
            services.AddHttpClient(TelegramSender.HttpClientName, c =>
            {
                c.BaseAddress = new Uri("https://api.telegram.org/");
                c.Timeout = TimeSpan.FromSeconds(30);
            })
            // Azure App Service outbound IPv6 dəstəkləmir, api.telegram.org isə AAAA qaytarır —
            // .NET default olaraq IPv6-ya ilişib 30s timeout alır. Yalnız IPv4 ilə qoşuluruq.
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                ConnectCallback = async (ctx, ct) =>
                {
                    var addresses = await Dns.GetHostAddressesAsync(
                        ctx.DnsEndPoint.Host, AddressFamily.InterNetwork, ct);
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };
                    try
                    {
                        await socket.ConnectAsync(addresses, ctx.DnsEndPoint.Port, ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            });
            services.AddScoped<ReceiptOcrJob>();
            services.AddScoped<PosBatchOperationJob>();
            services.AddScoped<IOtpGenerator, OtpGenerator>();
            services.AddScoped<IWalletCredentialProvider, WalletCredentialProvider>();
            services.AddScoped<IAppleWalletFactory, AppleWalletFactory>();
            services.AddSingleton<FileDownloader>();
            services.AddSingleton<IApplePassBuilder, ApplePassBuilder>();
            // Wallet imza sirləri (p12 / Google SA JSON): öz blob/CDN linkləri authenticated oxunur, public wwwroot-a yazılmır
            services.AddSingleton<IWalletSecretFetcher, WalletSecretFetcher>();
            services.AddSingleton<IApplePassSigner, ApplePassSigner>();
            services.AddScoped<IAppleWalletService, AppleWalletService>();
            services.AddScoped<ILoyaltyProgramService, LoyaltyProgramService>();
            services.AddScoped<IPasskitPushService, ApnsPasskitPushService>();
            services.AddScoped<IPasskitService, PasskitService>();
            services.AddScoped<ITransactionService, TransactionService>();
            services.AddScoped<IStatisticsService, StatisticsService>();
            services.AddScoped<IAnalyticsService, AnalyticsService>();
            services.AddScoped<IReferencePlaceService, ReferencePlaceService>();
            services.AddScoped<IWelcomeBonusService, WelcomeBonusService>();
            services.AddScoped<IFilePathResolver, FilePathResolver>();
            services.AddScoped<IUserCardService, UserCardService>();
            services.AddScoped<IDeviceTokenService, DeviceTokenService>();
            services.AddScoped<INotificationService, FcmNotificationService>();
            services.AddScoped<IUserNotificationService, UserNotificationService>();
            services.AddScoped<IWalletMessagingService, WalletMessagingService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IAppVersionService, AppVersionService>();
            services.AddScoped<IAppSettingService, AppSettingService>();
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
            // Bonus istisnaları (super admin) + iiko nomenklatura surəti: CRUD, sifariş resolver-i, Hangfire job (eyni instansiya)
            services.AddScoped<PosEarnExclusionService>();
            services.AddScoped<IPosEarnExclusionService>(sp => sp.GetRequiredService<PosEarnExclusionService>());
            services.AddScoped<IEarnExclusionResolver>(sp => sp.GetRequiredService<PosEarnExclusionService>());
            services.AddScoped<IPosNomenclatureJobs>(sp => sp.GetRequiredService<PosEarnExclusionService>());
            services.AddScoped<IPosObservedProductRecorder>(sp => sp.GetRequiredService<PosEarnExclusionService>());
            services.AddScoped<IPosLoyaltyDiscountTypeService, PosLoyaltyDiscountTypeService>();
            // iiko proqram qaydaları (read-only, tenant admin) — canlı iiko + 5 dəq cache
            services.AddScoped<IIikoRulesService, IikoRulesService>();
            services.AddScoped<IIikoConnectionResolver, IikoConnectionResolver>();
            services.AddScoped<IIikoCatalogService, IikoCatalogService>();
            services.AddScoped<IAmbassadorProgramFactory, AmbassadorProgramFactory>();
            services.AddScoped<IRewardCrudService, RewardCrudService>();
            services.AddScoped<IWalletWebhookService, WalletWebhookService>();
            services.AddScoped<IRkeeperLoyaltyService, RkeeperLoyaltyService>();
            services.AddScoped<IMonyoPosApiClient, MonyoPosApiClient>();
            services.AddScoped<IIikoPosApiClient, IikoPosApiClient>();
            services.AddScoped<ICustomerSyncService, CustomerSyncService>();
            services.AddScoped<ILotteryService, LotteryService>();
            services.AddScoped<IBranchService, BranchService>();
            services.AddScoped<ITenantStatusGuard, TenantStatusGuard>();
            services.AddScoped<ITenantSuspensionGuard, TenantSuspensionGuard>();
            services.AddScoped<ITenantSuspensionPassRefresher, TenantSuspensionPassRefresher>();
            services.AddSingleton<IMediaProcessor, MediaProcessor>();

            services.AddScoped<ISalesRepVisitService, SalesRepVisitService>();
            services.AddScoped<IPosSyncService, PosSyncService>();
            services.AddScoped<IPosCustomerIikoService, PosCustomerIikoService>();
            services.AddScoped<IPosCustomerImportService, PosCustomerImportService>();
            services.AddScoped<IBranchService, BranchService>();
            services.AddScoped<IRoomService, RoomService>();

            // === AI place-discovery + şəxsi hesab köməkçisi (chatbot) ===
            services.AddScoped<IPlaceSearchService, PlaceSearchService>();
            services.AddScoped<IMenuSearchService, MenuSearchService>();
            services.AddScoped<IKeywordGenerationService, KeywordGenerationService>();
            services.AddScoped<ICustomerAssistantTools, CustomerAssistantTools>();
            // Azure OpenAI ayarları: DB AppSettings (ai.openai.*) → config; 3 AI servisi eyni mənbədən oxuyur
            services.AddSingleton<IAzureOpenAiConfigProvider, AzureOpenAiConfigProvider>();
            services.AddScoped<IPlaceAssistantService, AzureOpenAiPlaceAssistantService>();
            services.AddScoped<IWalletPushJob, WalletPushJob>();
            services.AddScoped<IIikoWebhookFailureService, IikoWebhookFailureService>();
            services.AddScoped<IPointsExpiryService, PointsExpiryService>();
            // Bildirişlər v2: tenant admin servisi + Hangfire job-ları (eyni scoped instansiya)
            services.AddScoped<TenantNotificationService>();
            services.AddScoped<ITenantNotificationService>(sp => sp.GetRequiredService<TenantNotificationService>());
            services.AddScoped<INotificationJobs>(sp => sp.GetRequiredService<TenantNotificationService>());
            services.AddHttpClient(AzureOpenAiPlaceAssistantService.HttpClientName, c =>
            {
                c.Timeout = TimeSpan.FromSeconds(60);
            });

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
