using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Super admin-in qurduğu "link seçimi" səhifəsi (/l/{slug}): başlıq, logo, rənglər, fon, sıralı linklər/düymələr.
    /// Tenant-a bağlı deyil — istənilən sayda müstəqil səhifə.
    /// </summary>
    public class LinkPage : EntityBase, IEntity
    {
        /// <summary>Kiçik hərf, rəqəm, defis (unikal).</summary>
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? LogoUrl { get; set; }
        public string? BackgroundImageUrl { get; set; }
        /// <summary>Hex (#RRGGBB).</summary>
        public string BackgroundColor { get; set; } = "#0f172a";
        public string TextColor { get; set; } = "#ffffff";
        public string PrimaryColor { get; set; } = "#7c3aed";
        /// <summary>list | grid</summary>
        public string Layout { get; set; } = LinkPageLayouts.List;
        /// <summary>Alt yazı (məs. "Powered by Walvero"); boş = göstərilmir.</summary>
        public string? FooterText { get; set; }
        /// <summary>Yalnız dərc olunmuş səhifə public açılır.</summary>
        public bool IsPublished { get; set; } = true;

        public ICollection<LinkPageItem> Items { get; set; } = new List<LinkPageItem>();
    }

    public static class LinkPageLayouts
    {
        public const string List = "list";
        public const string Grid = "grid";
        public static readonly string[] All = { List, Grid };
    }

    public enum LinkPageItemKind
    {
        /// <summary>Seçim variantı (əsas linklər).</summary>
        Link = 1,
        /// <summary>Əlavə düymə (məs. "Əlaqə", "Instagram").</summary>
        Button = 2
    }

    /// <summary>Səhifədəki link və ya düymə.</summary>
    public class LinkPageItem : EntityBase, IEntity
    {
        public int LinkPageId { get; set; }
        public LinkPageItemKind Kind { get; set; } = LinkPageItemKind.Link;
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Url { get; set; } = string.Empty;
        /// <summary>solid | outline | ghost</summary>
        public string Style { get; set; } = LinkPageItemStyles.Solid;
        public string? IconUrl { get; set; }
        public int SortOrder { get; set; }

        public LinkPage LinkPage { get; set; } = null!;
    }

    public static class LinkPageItemStyles
    {
        public const string Solid = "solid";
        public const string Outline = "outline";
        public const string Ghost = "ghost";
        public static readonly string[] All = { Solid, Outline, Ghost };
    }

    /// <summary>Anonim klik (public səhifədən). İstifadəçi məlumatı saxlanmır.</summary>
    public class LinkPageClickEvent : EntityBase, IEntity
    {
        public int LinkPageId { get; set; }
        public int LinkPageItemId { get; set; }
        public DateTime ClickedAt { get; set; }

        public LinkPage LinkPage { get; set; } = null!;
    }
}
