using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Mobil app-dan bir linkə (menyu, sosial şəbəkə, Wolt/Bolt, telefon, lokasiya)
    /// keçidin anonim qeydi. İstifadəçi/kart saxlanmır — yalnız aggregate say üçün.
    /// </summary>
    public class LinkClickEvent : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // Tenant-səviyyə klik (kart detalı) üçün null; filial detalından gələndə dolu.
        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        // LinkClickTypes sabitlərindən biri (menu, instagram, wolt, boltfood, phone, ...)
        public string LinkType { get; set; } = string.Empty;

        public DateTime ClickedAt { get; set; }
    }

    /// <summary>Track olunan link tipləri — sərbəst string, referans üçün sabitlər.</summary>
    public static class LinkClickTypes
    {
        public const string Menu = "menu";
        public const string Instagram = "instagram";
        public const string Facebook = "facebook";
        public const string WhatsApp = "whatsapp";
        public const string Telegram = "telegram";
        public const string LinkedIn = "linkedin";
        public const string Tiktok = "tiktok";
        public const string TripAdvisor = "tripadvisor";
        public const string Website = "website";
        public const string Youtube = "youtube";
        public const string Twitter = "twitter";
        public const string Yelp = "yelp";
        public const string Threads = "threads";
        public const string Pinterest = "pinterest";
        public const string Wolt = "wolt";
        public const string BoltFood = "boltfood";
        public const string Phone = "phone";
        public const string Location = "location";
        public const string Waze = "waze";
    }
}
