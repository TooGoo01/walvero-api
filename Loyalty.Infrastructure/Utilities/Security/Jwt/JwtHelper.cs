using Loyalty.DataAccess.Entities.Users;
using Loyalty.Infrastructure.Extensions;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Loyalty.Infrastructure.Utilities.Security.Encrytion;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Loyalty.Infrastructure.Utilities.Security.Jwt
{
    public class JwtHelper : ITokenHelper
    {
        public IConfiguration Configuration { get; }
        private readonly TokenOptions _tokenOptions;
        private readonly DateTime _accessTokenExpiration;

        public JwtHelper(IConfiguration configuration)
        {
            Configuration = configuration;
            _tokenOptions = Configuration.GetSection(key: "TokenOptions").Get<TokenOptions>();
            _accessTokenExpiration = DateTime.UtcNow.AddHours(_tokenOptions.AccessTokenExpiration);
        }

        public AccessToken CreateToken(ApplicationUser user, IList<Claim> claims)
        {
            var securityKey = SecurityKeyHelper.CreateSecurityKey(_tokenOptions.SecurityKey);
            var signinCredentials = SigninCredentialsHelper.CreateSigninCredentials(securityKey);
            var jwt = CreateJwtSecurityToken(_tokenOptions, user, signinCredentials, claims);
            var jwtSecurityHandler = new JwtSecurityTokenHandler();
            var token = jwtSecurityHandler.WriteToken(jwt);

            var refreshToken = GenerateRefreshToken();
            var refreshTokenExpiration = DateTime.UtcNow.AddDays(_tokenOptions.RefreshTokenExpiration);

            return new AccessToken
            {
                Token = token,
                Expiration = _accessTokenExpiration,
                RefreshToken = refreshToken,
                RefreshTokenExpiration = refreshTokenExpiration
            };
        }

        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public JwtSecurityToken CreateJwtSecurityToken(TokenOptions tokenOptions, ApplicationUser user, SigningCredentials signingCredentials, IList<Claim> claims)
        {
            var jwt = new JwtSecurityToken
                (
                  issuer: _tokenOptions.Issuer,
                  audience: _tokenOptions.Audience,
                  expires: _accessTokenExpiration,
                  notBefore: DateTime.UtcNow,
                  claims: SetClaims(user, claims),  
                  signingCredentials: signingCredentials
                );

            return jwt;
        }

        private IEnumerable<Claim> SetClaims(ApplicationUser user, IList<Claim> claims)
        {
            // Burada artıq parametredən gələn claims-də
            // - Rollar
            // - tenant_id
            // - program_id
            // - is_guest
            // - is_profile_completed
            // var. Biz üstlərinə base user claim-ləri əlavə edirik.

            claims.AddNameIdentifier(user.Id.ToString());
            claims.AddEmail(user.Email);
            claims.AddName($"{user.Name} {user.Surname}");
            claims.AddUserName(user.UserName);
            claims.AddId(user.Id.ToString());

            // Guest status claims
            if (!claims.Any(x => x.Type == "is_guest"))
            {
                claims.Add(new Claim("is_guest", user.IsGuest.ToString().ToLower()));
            }

            if (!claims.Any(x => x.Type == "is_profile_completed"))
            {
                claims.Add(new Claim("is_profile_completed", user.IsProfileCompleted.ToString().ToLower()));
            }

            // Əgər tenant / program user obyektində də varsa və əlavə etmək istəyirsənsə
            if (user.TenantId != null && !claims.Any(x => x.Type == "tenant_id"))
            {
                claims.Add(new Claim("tenant_id", user.TenantId.ToString()!));
            }

            if (user.TenantId != null && user.Tenant?.LoyaltyPrograms?.FirstOrDefault() != null && !claims.Any(x => x.Type == "program_id"))
            {
                claims.Add(new Claim("program_id", user.Tenant.LoyaltyPrograms.FirstOrDefault().Id.ToString()!));
            }

            return claims;
        }

    }
}
