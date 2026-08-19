namespace Loyalty.Business.Dtos.Get.Main
{
    /// <summary>
    /// Bir geofence nöqtəsi — mobil app arxa planda region monitoring üçün istifadə edir.
    /// Mənbə: user-in əlavə etdiyi kart tenant-larının filialları (added) + premium tenant-lar (premium).
    /// </summary>
    public class GeofencePointDto
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
        public int RadiusMeters { get; set; }      // Branch.RadiusMeters, tenant-only üçün default
        public int TenantId { get; set; }
        public int? BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "added"; // "added" | "premium"
        public string? RelevantText { get; set; }   // bildiriş mətni
    }
}
