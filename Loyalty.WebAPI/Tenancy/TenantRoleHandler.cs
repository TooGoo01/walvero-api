//using LoyaltyWalletApp.Business.Services.Abstractions.Main;
//using Microsoft.AspNetCore.Authorization;
//using System.Security.Claims;

//namespace LoyaltyWalletApp.WEBUI.Tenancy
//{
//    public sealed class TenantRoleHandler : AuthorizationHandler<TenantRoleRequirement>
//    {
//        private readonly ITenantProvider _tenantProvider;
//        private readonly ITenantService _tenantService; // səndə bu servisin adı fərqli ola bilər

//        public TenantRoleHandler(ITenantService tenantService, ITenantProvider tenantProvider)
//        {
//            _tenantService = tenantService;
//            _tenantProvider = tenantProvider;
//        }

//        protected override async Task HandleRequirementAsync(
//            AuthorizationHandlerContext context,
//            TenantRoleRequirement requirement)
//        {
//            // Login yoxdursa – dayandır
//            var userIdStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
//            if (string.IsNullOrEmpty(userIdStr))
//                return;

//            var tenantId = _tenantProvider.CurrentTenantId;
//            if (tenantId is null)
//                return;

//            // DB-də: bu user həmin tenantda icazəli rollardan birinə malikdir?
//            var ok = await _tenantService.HasAnyRoleAsync(
//                userId: int.Parse(userIdStr),
//                tenantId: tenantId.Value,
//                roles: requirement.AllowedRoles);

//            if (ok.Success==true)
//                context.Succeed(requirement);
//            // yoxsa – succeed etmə (403 çıxacaq)
//        }
//    }
//}
