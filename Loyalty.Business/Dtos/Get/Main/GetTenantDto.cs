using Loyalty.Business.Dtos.Get.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetTenantDto
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Subdomain { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public bool RequireConfirmation { get; init; } = true; // tenant setting
        public int ConfirmationTtlMinutes { get; set; } = 10;
        public List<ApplicationUserDto> Admins { get; set; }
        public List<GetLoyaltyProgramDto> LoyaltyPrograms { get; set; }
        public int? categoryId { get; set; }
        public GetCategoryIdNameDto? Category { get; set; }
        
        public bool? IsCustomer { get; set; } = false;
    }
}
