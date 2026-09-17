using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>
    /// Seçilmiş menyu itemlərinin toplu redaktəsi. Yalnız DOLU sahələr tətbiq olunur.
    /// Qiymət/endirim yalnız dolu göndərildikdə tətbiq olunur (default: toxunulmur).
    /// Files doludursa itemlərin bütün mövcud şəkilləri bunlarla ƏVƏZ olunur.
    /// </summary>
    public class BulkUpdateMenuItemDto
    {
        public int TenantId { get; set; }

        [Required]
        public List<int> Ids { get; set; } = new();

        public string? AzName { get; set; }
        public string? EnName { get; set; }
        public string? RuName { get; set; }

        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }

        public string? PrepTimeMinutes { get; set; }

        /// <summary>Dolu göndərilsə itemlər bu kateqoriyaya köçürülür (filial uyğunluğu yoxlanılır).</summary>
        public int? MenuCategoryId { get; set; }

        /// <summary>true olsa MenuCategoryId ad kimi işlənir: hər item öz filialındakı eyni adlı kateqoriyaya köçürülür (o filialda yoxdursa yaradılır). Item filialı dəyişmir.</summary>
        public bool ApplyCategoryPerBranch { get; set; }

        /// <summary>Dolu göndərilsə BÜTÜN seçilmiş itemlərə tətbiq olunur (user bilərəkdən seçir).</summary>
        public double? Price { get; set; }
        public double? DiscountPrice { get; set; }

        public IFormFileCollection? Files { get; set; }

        public IFormFile? IngredientVideoFile { get; set; }
        public bool RemoveIngredientVideo { get; set; }
    }
}
