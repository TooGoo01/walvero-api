namespace Loyalty.Business.Dtos.Get.Main
{
    /// <summary>
    /// DTO for active country sequences
    /// </summary>
    public class GetCountrySequenceDto
    {
        public int CountryCode { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
        public string? Flag { get; set; }
    }
}

