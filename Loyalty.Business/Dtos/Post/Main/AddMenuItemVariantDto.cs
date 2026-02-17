using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddMenuItemVariantDto
    {
        [Required(ErrorMessage = "Kod məcburidir (məs: Regular, S, M, L)")]
        public string Code { get; set; } = string.Empty;

        public string? AzName { get; set; }
        public string? EnName { get; set; }
        public string? RuName { get; set; }

        public int SortOrder { get; set; } = 1;
        public bool IsDefault { get; set; }

        [Required(ErrorMessage = "Qiymət məcburidir")]
        public double Price { get; set; }

        public double DiscountPrice { get; set; }
    }
}
