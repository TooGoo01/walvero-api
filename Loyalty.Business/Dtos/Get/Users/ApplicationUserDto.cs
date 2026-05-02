using Loyalty.Business.Dtos.Get.Main;

namespace Loyalty.Business.Dtos.Get.Users
{
    public class ApplicationUserDto
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? Position { get; set; }
        public bool IsGuest { get; set; }
        public bool IsProfileCompleted { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int? TenantId { get; set; }
        public GetTenantDto? Tenant { get; set; }
        public List<string>? Roles { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
