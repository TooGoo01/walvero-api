using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// AI köməkçi üçün xarici məkan (restoran/kafe və s.) — Walvero tenant/branch DEYİL.
    /// App-da GÖRÜNMÜR; yalnız chatbot bilik bazasıdır. İstifadəçi soruşanda AI deyə bilər
    /// (amma "app-da yoxdur" kimi qeyd edir).
    /// </summary>
    public class AiReferencePlace : EntityBase, IEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }        // Restoran, Kafe və s.
        public string? City { get; set; }
        public string? Area { get; set; }            // məhəllə/ərazi (Nizami, Sahil...)
        public string? Address { get; set; }
        public string? Keywords { get; set; }        // vergüllə; AI axtarışı üçün
        public string? Description { get; set; }
        public string? Phone { get; set; }
        public string? MenuUrl { get; set; }

        /// <summary>
        /// Strukturlu menyu JSON (AI menyu suallarına cavab verə bilsin deyə).
        /// Format sərbəstdir; tövsiyə: {"categories":[{"name":"...","items":[{"name":"...","price":0}]}]}.
        /// MenuUrl user-ə göstərilən linkdir, bu isə AI bilik bazasıdır.
        /// </summary>
        public string? MenuJson { get; set; }

        /// <summary>Walvero tərəfindən sınanıb təsdiqlənib (məlumatların doğruluğu yoxlanılıb).</summary>
        public bool Verified { get; set; } = false;

        /// <summary>Məkanın loqosu (AppSetting/UploadImage ilə yüklənən URL) — AI cavab kartında göstərilir.</summary>
        public string? LogoUrl { get; set; }
    }
}
