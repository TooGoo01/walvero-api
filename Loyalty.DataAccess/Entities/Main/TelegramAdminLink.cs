using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Entities.Users;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Tenant adminin Telegram chat-i ilə əlaqəsi.
    /// Botdan gələn hər sorğuda TenantId yalnız bu sətirdən götürülür.
    /// IsActive = false → link ayrılıb (unlink).
    /// </summary>
    public class TelegramAdminLink : EntityBase, IEntity
    {
        public int ApplicationUserId { get; set; }
        public int TenantId { get; set; }
        public long ChatId { get; set; }
        public string? TelegramUsername { get; set; }
        public DateTime LinkedAt { get; set; }

        public ApplicationUser ApplicationUser { get; set; } = null!;
    }
}
