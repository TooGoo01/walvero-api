using System.Collections.Generic;
using System.Linq;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class TenantDiscoverDto
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public bool? IsCustomer { get; set; }
        public TenantDiscoverCategoryDto? Category { get; set; }
        public List<TenantDiscoverProgramDto> LoyaltyPrograms { get; set; } = new();
        public List<TenantDiscoverBranchDto> Branches { get; set; } = new();
    }

    public class TenantDiscoverBranchDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? PhotoUrl { get; set; }
        public string? LogoUrl { get; set; }
        public bool IsMainBranch { get; set; }
    }

    public class TenantDiscoverCategoryDto
    {
        public string? EnName { get; set; }
    }

    public class TenantDiscoverProgramDto
    {
        public int Id { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TenantDiscoverCardTemplateDto? CardTemplate { get; set; }
    }

    public class TenantDiscoverCardTemplateDto
    {
        public string? BackgroundColor { get; set; }
        public string? ForegroundColor { get; set; }
        public string? LogoUrl { get; set; }
        public string? GoogleLogoUrl { get; set; }
    }
}
