using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class UpdateMenuItemDto
    {
        [Required(ErrorMessage = "Id məcburidir")]
        public int Id { get; set; }

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
        public IFormFileCollection? Files { get; set; }
    }
}
