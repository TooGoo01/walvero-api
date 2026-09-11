using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddMenuItemDto
    {
        [Required(ErrorMessage = "Valyuta məcburidir")]
        public string Currency { get; set; }

        [Required(ErrorMessage = "Azərbaycan dilində ad məcburidir")]
        public string AzName { get; set; }

        [Required(ErrorMessage = "İngilis dilində ad məcburidir")]
        public string EnName { get; set; }

        [Required(ErrorMessage = "Rus dilində ad məcburidir")]
        public string RuName { get; set; }

        public string? AzDescription { get; set; }
        public string? RuDescription { get; set; }
        public string? EnDescription { get; set; }
        
        public string? PrepTimeMinutes { get; set; }

        [Required(ErrorMessage = "Valyuta işarəsi məcburidir")]
        public string CurrencySign { get; set; }

        [Required(ErrorMessage = "Qiymət məcburidir")]
        public double Price { get; set; }

        public double DiscountPrice { get; set; }

        [Required(ErrorMessage = "Menyu kateqoriyası məcburidir")]
        public int MenuCategoryId { get; set; }

        public int? BranchId { get; set; }

        /// <summary>
        /// Multi-filial yaratma: doludursa məhsul bu filialların HAMISINDA yaradılır
        /// (hərəsində ayrıca MenuItem). BranchId bu halda nəzərə alınmır.
        /// </summary>
        public List<int>? BranchIds { get; set; }

        public IFormFileCollection? Files { get; set; }

        // Redaktədə silinəcək mövcud şəkillərin id-ləri (boş/null = heç nə silinmir).
        public List<int>? RemovedImageIds { get; set; }

        public IFormFile? IngredientVideoFile { get; set; }

        public bool RemoveIngredientVideo { get; set; }
    }
}
