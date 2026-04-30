using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main.Branch
{
    public class UpdateBranchDto
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int? RadiusMeters { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(200)]
        public string? Email { get; set; }

        [MaxLength(500)]
        public string? PhotoUrl { get; set; }

        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        public string? WorkingHoursJson { get; set; }
        public bool? IsMainBranch { get; set; }

        [MaxLength(500)] public string? LocationUrl { get; set; }
        [MaxLength(500)] public string? WazeLocationUrl { get; set; }
        [MaxLength(500)] public string? MenuUrl { get; set; }
        [MaxLength(500)] public string? InstagramUrl { get; set; }
        [MaxLength(500)] public string? FacebookUrl { get; set; }
        [MaxLength(500)] public string? WhatsAppUrl { get; set; }
        [MaxLength(500)] public string? TelegramUrl { get; set; }
        [MaxLength(500)] public string? LinkedInUrl { get; set; }
    }
}
