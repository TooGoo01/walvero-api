using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Main
{
    public class GetAiReferencePlaceDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? City { get; set; }
        public string? Area { get; set; }
        public string? Address { get; set; }
        public string? Keywords { get; set; }
        public string? Description { get; set; }
        public string? Phone { get; set; }
        public string? MenuUrl { get; set; }
        public string? MenuJson { get; set; }
        public string? LogoUrl { get; set; }
        public bool IsActive { get; set; }
        public bool Verified { get; set; }
    }

    public class CreateAiReferencePlaceDto
    {
        [Required]
        [MaxLength(300)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(100)] public string? Category { get; set; }
        [MaxLength(100)] public string? City { get; set; }
        [MaxLength(150)] public string? Area { get; set; }
        [MaxLength(500)] public string? Address { get; set; }
        public string? Keywords { get; set; }
        public string? Description { get; set; }
        [MaxLength(50)] public string? Phone { get; set; }
        [MaxLength(500)] public string? MenuUrl { get; set; }
        /// <summary>Strukturlu menyu JSON — AI menyu suallarına cavab üçün (opsional).</summary>
        public string? MenuJson { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>Walvero tərəfindən sınanıb təsdiqlənib.</summary>
        public bool Verified { get; set; } = false;
        /// <summary>Loqo URL-i (AppSetting/UploadImage ilə yüklənir).</summary>
        [MaxLength(500)] public string? LogoUrl { get; set; }
    }

    public class UpdateAiReferencePlaceDto : CreateAiReferencePlaceDto
    {
        public int Id { get; set; }
    }
}
