namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetSalesRepVisitDto
    {
        public int Id { get; set; }
        public string? SalesRepName { get; set; }
        public string VenueName { get; set; } = string.Empty;
        public string? VenueAddress { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactName { get; set; }
        public string? Notes { get; set; }
        public int Status { get; set; }
        public string? StatusName { get; set; }
        public bool RemindFlag { get; set; }
        public DateTime? RemindAt { get; set; }
        public DateTime VisitDate { get; set; }
        public DateTime RegDate { get; set; }
        public bool IsActive { get; set; }
    }
}
