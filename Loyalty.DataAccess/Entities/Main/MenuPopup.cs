using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Public menyu (menu-app) açılanda göstərilən kampaniya popup-u.
    /// Tam dinamik: yalnız şəkil ola bilər, mətnli ola bilər; CTA (yönləndirmə) opsionaldır.
    /// </summary>
    public class MenuPopup : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public string? ImageUrl { get; set; }

        public string? AzTitle { get; set; }
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }

        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }

        /// <summary>Yönləndirmə düyməsi göstərilsin?</summary>
        public bool CtaEnabled { get; set; }

        /// <summary>Boşdursa default loyallıq deep-linki (walvero.com/join/{slug}) işlənir.</summary>
        public string? CtaUrl { get; set; }

        public string? AzCtaText { get; set; }
        public string? EnCtaText { get; set; }
        public string? RuCtaText { get; set; }

        /// <summary>Popup aktivdir? (IsActive soft-delete üçün saxlanılır)</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Ən kiçik SortOrder birinci göstərilir.</summary>
        public int SortOrder { get; set; }
    }
}
