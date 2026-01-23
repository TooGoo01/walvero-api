namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetTenantLocationDto
    {
        public int Id { get; set; }
        public int? tenantId { get; set; }
        public int? countryId { get; set; }
        public string? CountryName { get; set; }
        public int? cityId { get; set; }
        public string? CityName { get; set; }
        public int? timeZoneId { get; set; }
        public string? TimeZoneName { get; set; }
    }
}
