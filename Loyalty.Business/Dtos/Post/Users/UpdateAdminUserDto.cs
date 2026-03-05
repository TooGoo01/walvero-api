namespace Loyalty.Business.Dtos.Post.Users
{
    public class UpdateAdminUserDto
    {
        public string UserId { get; set; }
        public string? Role { get; set; }
        public int? TenantId { get; set; }
    }
}
