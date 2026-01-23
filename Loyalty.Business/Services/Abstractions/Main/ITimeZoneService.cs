

using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ITimeZoneService
    {
        // CRUD Operations
        Task<ServiceResult> AddTimeZone(AddTimeZoneDto timeZone);
        Task<ServiceResult> GetAllTimeZones();
        Task<ServiceResult> GetTimeZone(int id);
        Task<ServiceResult> UpdateTimeZone(AddTimeZoneDto timeZoneDto, int id);
        Task<ServiceResult> DeleteTimeZone(int id);

        // Timezone Utility Methods
        /// <summary>
        /// Gets the local DateTimeOffset for a tenant based on their timezone
        /// </summary>
        /// <param name="tenantId">The tenant ID</param>
        /// <param name="utcDateTime">The UTC datetime to convert (if null, uses DateTime.UtcNow)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>DateTimeOffset in the tenant's timezone</returns>
        Task<(DateTimeOffset localTime, string timeZoneId)> GetTenantLocalTimeAsync(
            int tenantId, 
            DateTime? utcDateTime = null, 
            CancellationToken cancellationToken = default);
    }
}
