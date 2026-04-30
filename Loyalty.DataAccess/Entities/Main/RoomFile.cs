namespace Loyalty.DataAccess.Entities.Main
{
    public class RoomFile : File
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public Room? Room { get; set; }

        public string MediaType { get; set; } = "image";
        public string? ThumbnailPath { get; set; }
        public int OrderIndex { get; set; } = 0;
    }
}
