using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Service for retrieving country sequence information
    /// </summary>
    public interface ICountrySequenceService
    {
        /// <summary>
        /// Gets all active country sequences
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>List of active country sequences</returns>
        Task<ServiceResult<List<GetCountrySequenceDto>>> GetActiveCountriesAsync(CancellationToken ct = default);
    }
}

