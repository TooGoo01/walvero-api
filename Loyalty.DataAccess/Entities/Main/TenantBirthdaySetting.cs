using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Enums;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Tenant-ın avtomatik ad günü bildirişi ayarı (tenant başına 1 sətir). Job "birthday-notifications" (15 dəq)
    /// tenant yerli vaxtı SendHourLocal-a çatanda ad günü [bu gün, bu gün + DaysBefore] aralığına düşən müştərilərə
    /// (hər ad günü üçün bir dəfə) göndərir. Aylıq bildiriş limitinə sayılmır.
    /// </summary>
    public class TenantBirthdaySetting : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public bool Enabled { get; set; }
        /// <summary>Tenant yerli saatı (0–23).</summary>
        public int SendHourLocal { get; set; } = 10;
        /// <summary>0 = ad günü günü, 1–14 = N gün əvvəl (hədiyyə göndəriş anında verilir, müddəti ad günündən hesablanır).</summary>
        public int DaysBefore { get; set; }
        /// <summary>Placeholder-lər: {ad} — müştərinin adı, {restoran} — şirkət adı, {hədiyyə}, {tarix} (dd.MM).</summary>
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool ToApp { get; set; } = true;
        public bool ToWallet { get; set; } = true;

        public bool GiftEnabled { get; set; }
        /// <summary>Points: ₼ bonus (xal/tier kartı); Stamps: möhür sayı; FreeItem: pulsuz məhsul sayı (möhür kartı).</summary>
        public BirthdayGiftType GiftType { get; set; } = BirthdayGiftType.Points;
        /// <summary>GiftType-a görə ₼ / möhür / məhsul sayı.</summary>
        public decimal GiftValue { get; set; }
        /// <summary>Yalnız Points: bonus ad günündən neçə gün sonra bitir (null = müddətsiz).</summary>
        public int? GiftExpiryDays { get; set; }
        /// <summary>{hədiyyə} üçün sərbəst mətn (≤100, məs. "1 kofe"); null = avtomatik ("5 ₼", "2 möhür", "1 Pulsuz içki").</summary>
        public string? GiftLabel { get; set; }

        /// <summary>Job-un bu tenant üçün son işlədiyi yerli tarix (gündə bir dəfə).</summary>
        public DateTime? LastRunLocalDate { get; set; }
    }
}
