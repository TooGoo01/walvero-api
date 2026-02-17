namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetRestorantWifiInformationDto
    {
        public int Id { get; set; }
        public string Password { get; set; }
        public string SSID { get; set; }
        public int TenantId { get; set; }
        public GetTenantDto? Tenant { get; set; }
    }
}
