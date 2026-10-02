using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Tenant-ın avtomatik ad günü bildirişi ayarı (tenant başına 1 sətir). Job "birthday-notifications" (15 dəq)
    /// tenant yerli vaxtı SendHourLocal-a çatanda o günün ad günü sahiblərinə göndərir. Aylıq bildiriş limitinə sayılmır.
    /// </summary>
    public class TenantBirthdaySetting : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public bool Enabled { get; set; }
        /// <summary>Tenant yerli saatı (0–23).</summary>
        public int SendHourLocal { get; set; } = 10;
        /// <summary>Placeholder-lər: {ad} — müştərinin adı, {restoran} — şirkət adı.</summary>
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool ToApp { get; set; } = true;
        public bool ToWallet { get; set; } = true;

        public bool GiftEnabled { get; set; }
        /// <summary>Xal kartında ₼ (xal), möhür kartında möhür sayı.</summary>
        public decimal GiftValue { get; set; }
        /// <summary>Yalnız xal kartı: bonus neçə gün sonra bitir (null = müddətsiz).</summary>
        public int? GiftExpiryDays { get; set; }

        /// <summary>Job-un bu tenant üçün son işlədiyi yerli tarix (gündə bir dəfə).</summary>
        public DateTime? LastRunLocalDate { get; set; }
    }
}
