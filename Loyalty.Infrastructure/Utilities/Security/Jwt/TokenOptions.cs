namespace Loyalty.Infrastructure.Utilities.Security.Jwt
{
    public class TokenOptions
    {
        public string Audience { get; set; }
        public string Issuer { get; set; }
        public int AccessTokenExpiration { get; set; }
        public int RefreshTokenExpiration { get; set; } = 30; // days, default 30 days
        public string SecurityKey { get; set; }
    }
}
