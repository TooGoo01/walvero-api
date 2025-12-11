using Loyalty.DataAccess.Entities.Users;
using System.Security.Claims;

namespace Loyalty.Infrastructure.Utilities.Security.Jwt
{
    public interface ITokenHelper
    {
        AccessToken CreateToken(ApplicationUser user, IList<Claim> claims);
        string GenerateRefreshToken();
    }
}
