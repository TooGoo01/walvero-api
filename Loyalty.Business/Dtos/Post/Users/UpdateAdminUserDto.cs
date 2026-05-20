namespace Loyalty.Business.Dtos.Post.Users
{
    public class UpdateAdminUserDto
    {
        public string UserId { get; set; } = null!;
        public string? Role { get; set; }
        public List<string>? Roles { get; set; }
        public int? TenantId { get; set; }
        public int? BranchId { get; set; } // null = tenant-wide; əks halda branch-scoped (Moderator / Branch Admin)
        public bool ClearBranchId { get; set; } = false; // true → BranchId = null (tenant-wide-yə qaldır)
    }
}
