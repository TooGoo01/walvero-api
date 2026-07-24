using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Telegram bota qoşulma üçün pending kod. Vaxt limiti yoxdur — kod istifadə
    /// olunana və ya yenisi yaradılana qədər qüvvədədir. Single-use: HandleStart
    /// istehlak edən kimi sətir silinir. Yeni kod yaradılanda user-in köhnə
    /// kodları silinir (bir user üçün yalnız bir pending kod).
    /// </summary>
    public class TelegramLinkCode : EntityBase, IEntity
    {
        public string Code { get; set; } = null!;
        public int ApplicationUserId { get; set; }
        public int TenantId { get; set; }
    }
}
