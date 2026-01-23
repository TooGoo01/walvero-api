namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetCityDto
    {
        public int Id { get; set; }
        public string AzName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public string RuName { get; set; } = string.Empty;
        public int countryId { get; set; }
        public string? CountryAzName { get; set; }
        public string? CountryEnName { get; set; }
        public string? CountryRuName { get; set; }
        
        // For backward compatibility
        public string Name => AzName;
        public string? CountryName => CountryAzName;
    }
}
