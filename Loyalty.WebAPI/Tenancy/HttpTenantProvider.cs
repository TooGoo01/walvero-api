//using LoyaltyWalletApp.Business.Services.Abstractions.Main;
//using LoyaltyWalletApp.DataAccess.Entities.Main;
//using Microsoft.AspNetCore.Http;
//using System.Collections;

//namespace LoyaltyWalletApp.WEBUI.Tenancy
//{
//    public sealed class HttpTenantProvider : ITenantProvider
//    {
//        private readonly IHttpContextAccessor _accessor;
//        public HttpTenantProvider(IHttpContextAccessor accessor) => _accessor = accessor;

//        // Slug/Subdomain (hər iki açarı dəstəklə)
//        public string? CurrentSubdomain
//        {
//            get
//            {
//                var items = _accessor.HttpContext?.Items;
//                if (items == null) return null;

//                if (items.TryGetValue("TenantSlug", out var s1) && s1 is string slug) return slug;
//                if (items.TryGetValue("TenantSubdomain", out var s2) && s2 is string sub) return sub;
//                return null;
//            }
//        }

//        public int? CurrentTenantId
//        {
//            get
//            {
//                var items = _accessor.HttpContext?.Items;
//                if (items == null) return null;

//                if (items.TryGetValue("TenantId", out var val))
//                {
//                    // boxed int → düzgün çıxarma
//                    if (val is int i) return i;
//                    if (val is int ni) return ni;
//                    if (val is string str && int.TryParse(str, out var parsed)) return parsed;
//                }
//                return null;
//            }
//        }

//        public Tenant? CurrentTenant
//        {
//            get
//            {
//                var items = _accessor.HttpContext?.Items;
//                if (items == null) return null;

//                if (items.TryGetValue("Tenant", out var t) && t is Tenant tenant) return tenant;
//                return null;
//            }
//        }
//    }
//}
