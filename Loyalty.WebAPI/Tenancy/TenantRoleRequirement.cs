using Microsoft.AspNetCore.Authorization;

namespace LoyaltyWalletApp.WEBUI.Tenancy
{
    public sealed class TenantRoleRequirement : IAuthorizationRequirement
    {
        public string[] AllowedRoles { get; }
        public TenantRoleRequirement(params string[] allowedRoles) => AllowedRoles = allowedRoles;
    }
}
