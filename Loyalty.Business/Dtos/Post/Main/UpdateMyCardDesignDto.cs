using Microsoft.AspNetCore.Http;

namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>
    /// Tenant admin kart DİZAYNINI (yalnız şəkillər + rənglər) yeniləyir.
    /// Loyalty konfiqi (steps/threshold/percent/reward/template adı) TOXUNULMUR.
    /// </summary>
    public sealed class UpdateMyCardDesignDto
    {
        public int LoyaltyProgramId { get; set; }

        // Şəkillər (file varsa yüklənir, yoxdursa mövcud url saxlanır/dəyişdirilir)
        public IFormFile? LogoFile { get; set; }
        public string? LogoUrl { get; set; }
        public IFormFile? GoogleLogoFile { get; set; }
        public string? GoogleLogoUrl { get; set; }
        public IFormFile? BackgroundImageFile { get; set; }
        public string? BackgroundImageUrl { get; set; }
        public IFormFile? StripImageFile { get; set; }
        public string? StripImageUrl { get; set; }
        public IFormFile? HeroImageFile { get; set; }
        public string? HeroImageUrl { get; set; }
        public IFormFile? AppLogoFile { get; set; }
        public string? AppLogoUrl { get; set; }
        public IFormFile? AppStripFile { get; set; }
        public string? AppStripUrl { get; set; }

        public IFormFile? BronzeIconFile { get; set; }
        public string? BronzeIconUrl { get; set; }
        public IFormFile? SilverIconFile { get; set; }
        public string? SilverIconUrl { get; set; }
        public IFormFile? GoldIconFile { get; set; }
        public string? GoldIconUrl { get; set; }
        public IFormFile? PlatinumIconFile { get; set; }
        public string? PlatinumIconUrl { get; set; }

        // Rənglər
        public string? BackgroundColor { get; set; }
        public string? ForegroundColor { get; set; }
        public string? LabelColor { get; set; }
        public string? ProgressBarColor { get; set; }
        public string? BronzeColor { get; set; }
        public string? SilverColor { get; set; }
        public string? GoldColor { get; set; }
        public string? PlatinumColor { get; set; }
    }
}
