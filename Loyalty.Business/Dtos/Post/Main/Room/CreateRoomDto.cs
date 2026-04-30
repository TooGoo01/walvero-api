using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main.Room
{
    public class CreateRoomDto
    {
        [Required]
        public int BranchId { get; set; }

        [Required]
        [MinLength(1)]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Type { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public int? Capacity { get; set; }

        /// <summary>
        /// Minimum depozit məbləği. Null/buraxılan → depozit yoxdur.
        /// </summary>
        public decimal? MinDeposit { get; set; }

        public int? OrderIndex { get; set; }
    }
}
