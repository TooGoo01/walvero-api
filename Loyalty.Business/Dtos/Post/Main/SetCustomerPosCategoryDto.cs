using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>Müştərinin kassa endirim səviyyəsi: iiko qonaq kateqoriyası (kataloq discountTiers-dən). Boş = səviyyəni sil.</summary>
    public class SetCustomerPosCategoryDto
    {
        [Required]
        public int CustomerId { get; set; }

        [MaxLength(64)]
        public string? CategoryId { get; set; }
    }
}
