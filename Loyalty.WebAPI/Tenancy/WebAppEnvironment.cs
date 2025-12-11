using Loyalty.Business.Services.Abstractions.Main;

namespace Loyalty.WebAPI.Tenancy
{
    public sealed class WebAppEnvironment : IAppEnvironment
    {
        private readonly IWebHostEnvironment _web;
        public WebAppEnvironment(IWebHostEnvironment web) => _web = web;
        public string WebRootPath => _web.WebRootPath;
    }
}
