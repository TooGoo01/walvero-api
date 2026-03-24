namespace Loyalty.Business.Dtos.Get.Branch
{
    public class GetBranchDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string Name { get; set; }
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int RadiusMeters { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PhotoUrl { get; set; }
        public string? WorkingHoursJson { get; set; }
        public bool IsMainBranch { get; set; }
        public bool IsActive { get; set; }
        public DateTime RegDate { get; set; }
        public int StaffCount { get; set; }
        public double? DistanceKm { get; set; } // for nearby queries
    }
}
