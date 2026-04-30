namespace Loyalty.Business.Dtos.Get.Main.Room
{
    public class GetRoomFileDto
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string MediaType { get; set; } = "image";
        public string? ThumbnailUrl { get; set; }
        public int OrderIndex { get; set; }
    }
}
