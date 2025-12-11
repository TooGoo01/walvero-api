//using LoyaltyWalletApp.Business.Services.Abstractions.Main;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Filters;

//namespace LoyaltyWalletApp.WEBUI.Tenancy
//{
//    public sealed class TenantBrandingFilter : IAsyncActionFilter
//    {
//        private readonly ITenantProvider _tp;
//        private readonly ITenantBrandingService _branding;

//        public TenantBrandingFilter(ITenantProvider tp, ITenantBrandingService branding)
//        {
//            _tp = tp; _branding = branding;
//        }

//        public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
//        {
//            var tenantId = _tp.CurrentTenantId ?? 0;

//            if (tenantId > 0)
//            {
//                var res = await _branding.GetByTenantId(tenantId);
//                if (res.Success && res.Data is not null && ctx.Controller is Controller c)
//                {
//                    // Views + Layout üçün:
//                    c.ViewBag.Brand = res.Data; // GetTenantBrandingDto
//                }
//            }

//            await next();
//        }
//    }
//    }
