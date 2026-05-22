using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class UpdateMenuCategoryDto
    {
        [Required(ErrorMessage = "Id məcburidir")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Azərbaycan dilində ad məcburidir")]
        public string AzName { get; set; }

        [Required(ErrorMessage = "İngilis dilində ad məcburidir")]
        public string EnName { get; set; }

        [Required(ErrorMessage = "Rus dilində ad məcburidir")]
        public string RuName { get; set; }

        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }

        public int? BranchId { get; set; }
    }
}
