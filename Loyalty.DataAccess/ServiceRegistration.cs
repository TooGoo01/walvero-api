using Loyalty.DataAccess.Repositories;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Sequences;
using Loyalty.DataAccess.Repositories.Implementations.Main;
using Loyalty.DataAccess.Repositories.Implementations.Sequences;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.Extensions.DependencyInjection;

namespace Loyalty.DataAccess
{
    public static class ServiceRegistration
    {
        public static void AddDataAccessServices(this IServiceCollection services)
        {
            //var cc = Configuration.ConnectionString;


            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IServiceRepository, ServiceRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IContactRepository, ContactRepository>();
            services.AddScoped<ISalesRepVisitRepository, SalesRepVisitRepository>();
            services.AddScoped<IPartnerRepository, PartnerRepository>();
            services.AddScoped<IAboutRepository, AboutRepository>();
            services.AddScoped<IWalletCredentialRepository, WalletCredentialRepository>();
            services.AddScoped<IWalletPassRepository, WalletPassRepository>();
            services.AddScoped<IWalveroWalletPassRepository, WalveroWalletPassRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<ITenantBrandingRepository, TenantBrandingRepository>();
            services.AddScoped<ILoyaltyProgramRepository, LoyaltyProgramRepository>();
            services.AddScoped<IHappyHoursWindowRepository, HappyHoursWindowRepository>();
            services.AddScoped<ILoyaltyCardRepository, LoyaltyCardRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<ICardTemplateRepository, CardTemplateRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IAppVersionRepository, AppVersionRepository>();
            services.AddScoped<IOfferRepository, OfferRepository>();
            services.AddScoped<IStoryRepository, StoryRepository>();
            services.AddScoped<ISequenceRepository, SequenceRepository>();
            
            // Menu related repositories
            services.AddScoped<IMenuCategoryRepository, MenuCategoryRepository>();
            services.AddScoped<IMenuItemRepository, MenuItemRepository>();
            services.AddScoped<IMenuItemImageRepository, MenuItemImageRepository>();
            services.AddScoped<IRestorantWifiInformationRepository, RestorantWifiInformationRepository>();
            services.AddScoped<ITenantLinkRepository, TenantLinkRepository>();
            services.AddScoped<IIntegrationTypeRepository, IntegrationTypeRepository>();
            services.AddScoped<ITenantIntegrationRepository, TenantIntegrationRepository>();
            services.AddScoped<IPosProductEarnRuleRepository, PosProductEarnRuleRepository>();
            services.AddScoped<IPosLoyaltyDiscountTypeRepository, PosLoyaltyDiscountTypeRepository>();
            services.AddScoped<IPosCustomerMappingRepository, PosCustomerMappingRepository>();
            services.AddScoped<IRewardRepository, RewardRepository>();
            services.AddScoped<ILotteryRepository, LotteryRepository>();
            services.AddScoped<IBranchRepository, BranchRepository>();
            services.AddScoped<ITelegramAdminLinkRepository, TelegramAdminLinkRepository>();
            services.AddScoped<ITelegramLinkCodeRepository, TelegramLinkCodeRepository>();
            services.AddScoped<ISalesRepVisitRepository, SalesRepVisitRepository>();

        }
    }
}
