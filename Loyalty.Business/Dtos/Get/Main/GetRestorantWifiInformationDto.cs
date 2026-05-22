namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetRestorantWifiInformationDto
    {
        public int Id { get; set; }
        public string Password { get; set; }
        public string SSID { get; set; }
        public int TenantId { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public GetTenantDto? Tenant { get; set; }
    }
}
