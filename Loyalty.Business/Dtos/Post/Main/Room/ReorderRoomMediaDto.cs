using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main.Room
{
    public class ReorderRoomMediaDto
    {
        [Required]
        public List<RoomMediaOrderItem> Items { get; set; } = new();
    }

    public class RoomMediaOrderItem
    {
        [Required]
        public int FileId { get; set; }

        [Required]
        public int OrderIndex { get; set; }
    }
}
