namespace Loyalty.Business.Dtos.Post.Users
{
    public class CreateAdminUserDto
    {
        public string? FullName { get; set; }
        public string PhoneNumber { get; set; } = null!;
        public string? Email { get; set; }
        public string Password { get; set; } = null!;
        public string? Role { get; set; }
        public List<string>? Roles { get; set; }
        public int? TenantId { get; set; }
    }
}
