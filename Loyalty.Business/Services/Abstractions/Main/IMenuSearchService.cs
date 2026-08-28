using Loyalty.Business.Dtos.Chatbot;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IMenuSearchService
    {
        /// <summary>Yeməyə/məhsula görə məkan axtarışı (AI `search_menu_items` tool-u).</summary>
        Task<List<MenuItemResultDto>> SearchAsync(MenuItemSearchQuery query, CancellationToken ct = default);
    }
}
