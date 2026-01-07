using Loyalty.DataAccess.Repositories;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Implementations.Main;
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
            services.AddScoped<IPartnerRepository, PartnerRepository>();
            services.AddScoped<IAboutRepository, AboutRepository>();
            services.AddScoped<IWalletCredentialRepository, WalletCredentialRepository>();
            services.AddScoped<IWalletPassRepository, WalletPassRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<ITenantRepository, TenantRepository>();
            services.AddScoped<ITenantBrandingRepository, TenantBrandingRepository>();
            services.AddScoped<ILoyaltyProgramRepository, LoyaltyProgramRepository>();
            services.AddScoped<ILoyaltyCardRepository, LoyaltyCardRepository>();
            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<ICardTemplateRepository, CardTemplateRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IAppVersionRepository, AppVersionRepository>();
            services.AddScoped<ISequenceRepository, SequenceRepository>();

        }
    }
}
