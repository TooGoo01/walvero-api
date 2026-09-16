using Loyalty.Business.Dtos.Chatbot;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IMenuSearchService
    {
        /// <summary>Yeməyə/məhsula görə məkan axtarışı (AI `search_menu_items` tool-u).</summary>
        Task<List<MenuItemResultDto>> SearchAsync(MenuItemSearchQuery query, CancellationToken ct = default);

        /// <summary>
        /// Konkret məkanın menyu icmalı (AI `get_place_menu` tool-u):
        /// categoryName boşdursa kateqoriya üzrə qiymət aralıqları, doludursa
        /// həmin kateqoriyanın itemləri. Məkan tapılmasa null.
        /// </summary>
        Task<object?> GetPlaceMenuAsync(string placeName, string? categoryName, CancellationToken ct = default);
    }
}
