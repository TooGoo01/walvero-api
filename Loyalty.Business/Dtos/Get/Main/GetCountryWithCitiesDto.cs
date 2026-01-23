namespace Loyalty.Business.Dtos.Get.Main
{
    /// <summary>
    /// Country DTO with nested cities list
    /// </summary>
    public class GetCountryWithCitiesDto
    {
        public int Id { get; set; }
        public string AzName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public string RuName { get; set; } = string.Empty;
        
        /// <summary>
        /// List of cities in this country
        /// </summary>
        public List<GetCityDto> Cities { get; set; } = new List<GetCityDto>();
        
        // For backward compatibility
        public string Name => AzName;
    }
}
