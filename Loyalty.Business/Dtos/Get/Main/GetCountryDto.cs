namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetCountryDto
    {
        public int Id { get; set; }
        public string AzName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public string RuName { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? PhoneCode { get; set; }
        public List<GetCityDto>? Cities { get; set; }

        // For backward compatibility
        public string Name => AzName;
    }
}
