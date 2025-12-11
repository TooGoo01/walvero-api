using Loyalty.Business.Dtos.Get.Main;

namespace Loyalty.Business.Dtos.Get.Users
{
    public class ApplicationUserDto
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Position { get; set; }
        public GetTenantDto Tenant { get; set; }
        public List<string>? Roles { get; set; }
    }
}
