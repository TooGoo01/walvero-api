using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    public class AppSetting : EntityBase, IEntity
    {
        public string Key { get; set; } = default!;
        public string Value { get; set; } = default!;
        public DateTime? UpdatedAt { get; set; }
    }
}
