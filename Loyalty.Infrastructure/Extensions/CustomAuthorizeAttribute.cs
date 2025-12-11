using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Infrastructure.Extensions
{
    public class CustomAuthorizeAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        readonly string[] _requiredClaims;

        public CustomAuthorizeAttribute(params string[] claims)
        {
            _requiredClaims = claims;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var isAuthenticated = context.HttpContext.User.Identity.IsAuthenticated;
            if (!isAuthenticated)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            //claim type a görə check
            //var hasAllRequredClaims = _requiredClaims.All(claim => context.HttpContext.User.HasClaim(x => x.Type == claim));
            var cc = false;
            foreach (var item in _requiredClaims)
            {
                if (context.HttpContext.User.HasClaim(x => x.Value == item))
                {
                    cc = true;
                    break;
                }
            }
            //var hasAllRequredClaims = _requiredClaims.All(claim => context.HttpContext.User.HasClaim(x => x.Value == claim));
            //if (!hasAllRequredClaims)
            //{
            //    context.Result = new ForbidResult();
            //    return;
            //}

            if (!cc)
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}
