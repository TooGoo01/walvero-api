using Loyalty.Business.Dtos.Chatbot;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// AI köməkçinin "search_places" tool-u — keyword + geo əsasında tenant/filial kataloqunda axtarış.
    /// </summary>
    public interface IPlaceSearchService
    {
        Task<List<PlaceResultDto>> SearchAsync(PlaceSearchQuery query, CancellationToken ct = default);
    }
}
