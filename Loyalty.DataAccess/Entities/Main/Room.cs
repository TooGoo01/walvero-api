using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System.Collections.Generic;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Room : EntityBase, IEntity
    {
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? Description { get; set; }
        public int? Capacity { get; set; }
        public int OrderIndex { get; set; } = 0;

        /// <summary>
        /// Otaq üçün minimum depozit məbləği. Null → depozit tələb olunmur.
        /// </summary>
        public decimal? MinDeposit { get; set; }

        public ICollection<RoomFile> Files { get; set; } = new List<RoomFile>();
    }
}
