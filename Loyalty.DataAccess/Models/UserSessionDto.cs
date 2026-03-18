

namespace Loyalty.DataAccess.Models
{
    public class UserSessionDto
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int? ProgramId { get; set; }
        public long? WalveroId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Token { get; set; }
        public DateTime? Expiration { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiration { get; set; }
        public List<string>? Roles { get; set; }
        public string? GoogleLogoUrl { get; set; }
        public List<ProgramSummaryDto>? Programs { get; set; }
    }

    public class ProgramSummaryDto
    {
        public int Id { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public int ProgramType { get; set; }
    }
}
