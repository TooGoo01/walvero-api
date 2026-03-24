namespace Loyalty.Business.Dtos.Post.Branch
{
    public class UpdateBranchDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int RadiusMeters { get; set; } = 500;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PhotoUrl { get; set; }
        public string? WorkingHoursJson { get; set; }
        public bool IsMainBranch { get; set; } = false;
    }
}
