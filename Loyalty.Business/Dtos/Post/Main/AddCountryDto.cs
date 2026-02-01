using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddCountryDto
    {
        [Required(ErrorMessage = "Azərbaycan dilində ad məcburidir")]
        public string AzName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "İngilis dilində ad məcburidir")]
        public string EnName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Rus dilində ad məcburidir")]
        public string RuName { get; set; } = string.Empty;
    }
}
