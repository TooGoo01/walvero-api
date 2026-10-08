using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Bir bildiriş göndərişinin (TenantNotificationLog) bir alıcısı — real çatdırılma nəticəsi + geri dönüş attribution-u
    /// (göndərişdən sonrakı N gündə ziyarət/dövriyyə). RegDate = göndəriş vaxtı (UTC). Manual/planlı/ad günü hamısı yazır.
    /// </summary>
    public class TenantNotificationRecipient : EntityBase, IEntity
    {
        public int TenantNotificationLogId { get; set; }
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        /// <summary>Ad günü: hədiyyə yazılan kart; digər mənbələrdə null.</summary>
        public int? LoyaltyCardId { get; set; }

        /// <summary>App hesabı var (FCM hədəfi).</summary>
        public bool AppTarget { get; set; }
        /// <summary>Ən azı bir cihaza FCM uğurla qəbul edildi.</summary>
        public bool AppSent { get; set; }
        /// <summary>Aktiv kartı var (wallet hədəfi).</summary>
        public bool WalletTarget { get; set; }
        /// <summary>Ən azı bir Google/Apple kanalı uğurlu oldu.</summary>
        public bool WalletSent { get; set; }
    }
}
