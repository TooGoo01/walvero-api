namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetCountryDto
    {
        public int Id { get; set; }
        public string AzName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public string RuName { get; set; } = string.Empty;
        
        // For backward compatibility
    
    }
}
