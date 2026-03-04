using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Loyalty.Infrastructure.Utilities.Security.Jwt;
using Microsoft.Extensions.DependencyInjection;

namespace Loyalty.Infrastructure
{
    public static class ServiceRegistration
    {
        public static void AddInfrastructureServices(this IServiceCollection services)
        {
            services.AddScoped<ITokenHelper, JwtHelper>();
            services.AddScoped<ISensitiveDataProtector, SensitiveDataProtector>();
            services.AddScoped<IWalletTokenGenerator, WalletTokenGenerator>();
        }
    }
}
