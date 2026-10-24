using System.ComponentModel.DataAnnotations;
using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Dtos.Main
{
    public class LinkPageItemDto
    {
        /// <summary>0 / null = yeni; dolu = mövcud sətir yenilənir (klik statistikası qorunur).</summary>
        public int? Id { get; set; }
        public LinkPageItemKind Kind { get; set; } = LinkPageItemKind.Link;
        [Required, MaxLength(120)] public string Label { get; set; } = string.Empty;
        [MaxLength(300)] public string? Description { get; set; }
        [Required, MaxLength(1000)] public string Url { get; set; } = string.Empty;
        [MaxLength(20)] public string Style { get; set; } = LinkPageItemStyles.Solid;
        [MaxLength(1000)] public string? IconUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CreateLinkPageDto
    {
        [Required, MaxLength(64)] public string Slug { get; set; } = string.Empty;
        [Required, MaxLength(150)] public string Title { get; set; } = string.Empty;
        [MaxLength(300)] public string? Subtitle { get; set; }
        [MaxLength(1000)] public string? LogoUrl { get; set; }
        [MaxLength(1000)] public string? BackgroundImageUrl { get; set; }
        [MaxLength(9)] public string BackgroundColor { get; set; } = "#0f172a";
        [MaxLength(9)] public string TextColor { get; set; } = "#ffffff";
        [MaxLength(9)] public string PrimaryColor { get; set; } = "#7c3aed";
        [MaxLength(20)] public string Layout { get; set; } = LinkPageLayouts.List;
        [MaxLength(150)] public string? FooterText { get; set; }
        public bool IsPublished { get; set; } = true;
        public List<LinkPageItemDto> Items { get; set; } = new();
    }

    public class UpdateLinkPageDto : CreateLinkPageDto
    {
        public int Id { get; set; }
    }

    public class GetLinkPageItemDto
    {
        public int Id { get; set; }
        public LinkPageItemKind Kind { get; set; }
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Style { get; set; } = LinkPageItemStyles.Solid;
        public string? IconUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Yalnız admin cavabında (ümumi klik sayı).</summary>
        public int ClickCount { get; set; }
    }

    public class GetLinkPageDto
    {
        public int Id { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? LogoUrl { get; set; }
        public string? BackgroundImageUrl { get; set; }
        public string BackgroundColor { get; set; } = string.Empty;
        public string TextColor { get; set; } = string.Empty;
        public string PrimaryColor { get; set; } = string.Empty;
        public string Layout { get; set; } = string.Empty;
        public string? FooterText { get; set; }
        public bool IsPublished { get; set; }
        public bool IsActive { get; set; }
        public DateTime RegDate { get; set; }
        public DateTime? EditDate { get; set; }
        /// <summary>Admin: hamısı (sıra ilə); public: yalnız aktivlər.</summary>
        public List<GetLinkPageItemDto> Items { get; set; } = new();
        /// <summary>Admin siyahısı üçün ümumi klik.</summary>
        public int TotalClicks { get; set; }
    }

    public class LinkPageStatsDto
    {
        public int Total { get; set; }
        public List<LinkPageItemStatDto> ByItem { get; set; } = new();
        public List<LinkPageDayStatDto> Series { get; set; } = new();
    }

    public class LinkPageItemStatDto
    {
        public int ItemId { get; set; }
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class LinkPageDayStatDto
    {
        public DateOnly Date { get; set; }
        public int Count { get; set; }
    }
}
