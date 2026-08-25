using System.Collections.Generic;
using System.Linq;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class TenantDiscoverDto
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        // In-app menyu üçün: slug = Tenant.Subdomain (public menu endpoint açarı)
        public string? Slug { get; set; }
        public bool IsHasMenu { get; set; }
        public bool? IsCustomer { get; set; }
        // Tenant-ın öz koordinatı — filialsız tenant-lar xəritədə bununla göstərilir
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public TenantDiscoverCategoryDto? Category { get; set; }
        public List<TenantDiscoverProgramDto> LoyaltyPrograms { get; set; } = new();
        public List<TenantDiscoverBranchDto> Branches { get; set; } = new();
    }

    public class TenantDiscoverBranchDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        // In-app menyu: per-branch menyu endpoint açarı + görünüş override-ı
        public string? Slug { get; set; }
        public string? DefaultMenuView { get; set; }
        public string? AppDisplayName { get; set; } // doludursa app yalnız bunu göstərir
        public string? Address { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? PhotoUrl { get; set; }
        public string? LogoUrl { get; set; }
        public bool IsMainBranch { get; set; }

        public string? Phone { get; set; }
        public string? LocationUrl { get; set; }
        public string? WazeLocationUrl { get; set; }
        public string? MenuUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? FacebookUrl { get; set; }
        public string? WhatsAppUrl { get; set; }
        public string? TelegramUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? TiktokUrl { get; set; }
        public string? TripAdvisorUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? YoutubeUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? YelpUrl { get; set; }
        public string? ThreadsUrl { get; set; }
        public string? PinterestUrl { get; set; }
        public string? WoltUrl { get; set; }
        public string? BoltFoodUrl { get; set; }

        public string? BackgroundColor { get; set; }
        public string? ForegroundColor { get; set; }
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
        public string? AppLogoUrl { get; set; }
        public string? AppStripUrl { get; set; }

        // Discover kartında reward/bonus göstərmək üçün.
        // TemplateType: 1=ProgressBased, 2=PointsBased, 3=TierBased.
        public int TemplateType { get; set; }
        public int TotalSteps { get; set; }
        public string? StepLabel { get; set; }
        public string? FreeRewardLabel { get; set; }
        public string? RewardDescription { get; set; }
        public int? BronzePercent { get; set; }
        public int? BronzePercentCash { get; set; } // nağd cashback faizi — badge "X% / Y%" üçün
    }
}
