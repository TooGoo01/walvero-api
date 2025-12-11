using Microsoft.IdentityModel.Tokens;

namespace Loyalty.Infrastructure.Utilities.Security.Encrytion
{
    public static class SigninCredentialsHelper
    {
        public static SigningCredentials CreateSigninCredentials(SecurityKey securityKey)
        {
            return new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);
        }
    }
}
