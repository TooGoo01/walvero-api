using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    // Toplu redaktə: null sahə toxunulmur, dəyər verilən sahə bütün Ids-ə yazılır
    public class BulkUpdateMenuCategoryDto
    {
        public int TenantId { get; set; }

        [Required(ErrorMessage = "Id siyahısı məcburidir")]
        public List<int> Ids { get; set; } = new();

        public string? AzName { get; set; }
        public string? EnName { get; set; }
        public string? RuName { get; set; }

        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }

        public string? DescriptionColor { get; set; }
        public int? DescriptionFontSize { get; set; }
    }
}
