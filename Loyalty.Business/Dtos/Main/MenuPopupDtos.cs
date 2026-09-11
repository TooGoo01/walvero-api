namespace Loyalty.Business.Dtos.Main
{
    /// <summary>Tenant admin CRUD üçün menyu popup DTO-su. Id=null → yeni.</summary>
    public class SaveMenuPopupDto
    {
        public int? Id { get; set; }
        public string? ImageUrl { get; set; }
        public string? AzTitle { get; set; }
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }
        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }
        public bool CtaEnabled { get; set; }
        public string? CtaUrl { get; set; }
        public string? AzCtaText { get; set; }
        public string? EnCtaText { get; set; }
        public string? RuCtaText { get; set; }
        public bool IsEnabled { get; set; } = true;
        public int SortOrder { get; set; }
    }

    public class GetMenuPopupDto : SaveMenuPopupDto
    {
        public new int Id { get; set; }
    }

    /// <summary>Menu-app-ın oxuduğu public forma. ctaUrl həmişə doldurulur (default join linki).</summary>
    public class PublicMenuPopupDto
    {
        public string? ImageUrl { get; set; }
        public string? AzTitle { get; set; }
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }
        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }
        public bool CtaEnabled { get; set; }
        public string? CtaUrl { get; set; }
        public string? AzCtaText { get; set; }
        public string? EnCtaText { get; set; }
        public string? RuCtaText { get; set; }
    }
}
