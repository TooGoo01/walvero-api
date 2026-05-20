namespace Loyalty.Business.Dtos.Get.Users
{
    public class GuestLoginResponseDto
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int? ProgramId { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public string? UserName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiration { get; set; }
        public List<string>? Roles { get; set; }
        public bool IsGuest { get; set; }
        public bool IsProfileCompleted { get; set; }
        public string? GoogleLogoUrl { get; set; }
        
        // Legacy property for backward compatibility
        public int UserId 
        { 
            get => Id; 
            set => Id = value; 
        }
    }
}

