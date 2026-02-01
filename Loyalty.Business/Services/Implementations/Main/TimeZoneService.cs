using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class TimeZoneService : ITimeZoneService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly ILogger<TimeZoneService> _logger;

        public TimeZoneService(
            IMapper mapper,
            IUnitOfWork unitOfWork,
            LoyaltyWalletDbContext dbContext,
            ILogger<TimeZoneService> logger)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _dbContext = dbContext;
            _logger = logger;
        }

        // CRUD Operations
        public async Task<ServiceResult> AddTimeZone(AddTimeZoneDto timeZone)
        {
            var request = _mapper.Map<DataAccess.Entities.Main.TimeZone>(timeZone);
            await _unitOfWork.Repository<DataAccess.Entities.Main.TimeZone>().AddAsync(request);
            _unitOfWork.Commit();
            var response = _mapper.Map<GetTimeZoneDto>(request);
            return new ServiceResult(true, response.Id);
        }

        public async Task<ServiceResult> GetAllTimeZones()
        {
            var timeZones = await _unitOfWork.Repository<DataAccess.Entities.Main.TimeZone>().GetAllAsync();
            if (timeZones != null)
            {
                var response = _mapper.Map<ICollection<GetTimeZoneDto>>(timeZones);
                return new ServiceResult(true, response);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetTimeZone(int id)
        {
            try
            {
                var result = await _unitOfWork.Repository<DataAccess.Entities.Main.TimeZone>().GetByIdAsync(id);
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = _mapper.Map<GetTimeZoneDto>(result);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> UpdateTimeZone(AddTimeZoneDto timeZoneDto, int id)
        {
            var timeZone = await _unitOfWork.Repository<DataAccess.Entities.Main.TimeZone>().GetByIdAsync(id);
            if (timeZone != null)
            {
                if (!string.IsNullOrWhiteSpace(timeZoneDto.Name))
                {
                    timeZone.Name = timeZoneDto.Name.Trim();
                }

                _unitOfWork.Repository<DataAccess.Entities.Main.TimeZone>().Update(timeZone);
                _unitOfWork.Commit();
                var response = _mapper.Map<GetTimeZoneDto>(timeZone);
                return new ServiceResult(true, response.Id);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> DeleteTimeZone(int id)
        {
            var timeZone = await _unitOfWork.Repository<DataAccess.Entities.Main.TimeZone>().GetByIdAsync(id);
            if (timeZone != null)
            {
                _unitOfWork.Repository<DataAccess.Entities.Main.TimeZone>().Delete(timeZone);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }

        // Timezone Utility Method
        public async Task<(DateTimeOffset localTime, string timeZoneId)> GetTenantLocalTimeAsync(
            int tenantId, 
            DateTime? utcDateTime = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Use provided time or current UTC time
                var utcTime = utcDateTime ?? DateTime.UtcNow;
                
                // Ensure the time is in UTC
                if (utcTime.Kind != DateTimeKind.Utc)
                {
                    utcTime = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
                }

                // Get tenant with location using DbContext
                var tenant = await _dbContext.Tenants
                    .Include(t => t.TenantLocation)
                        .ThenInclude(tl => tl.TimeZone)
                    .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

                string timeZoneId = "UTC";
                TimeZoneInfo timeZone;

                // Get timezone from tenant location if available
                if (tenant?.TenantLocation?.TimeZone?.Name != null && 
                    !string.IsNullOrWhiteSpace(tenant.TenantLocation.TimeZone.Name))
                {
                    timeZoneId = tenant.TenantLocation.TimeZone.Name;
                    
                    try
                    {
                        // Try to find the timezone
                        timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                    }
                    catch (TimeZoneNotFoundException)
                    {
                        _logger.LogWarning("TimeZone not found: {TimeZoneId}. Using UTC. TenantId: {TenantId}", 
                            timeZoneId, tenantId);
                        timeZoneId = "UTC";
                        timeZone = TimeZoneInfo.Utc;
                    }
                }
                else
                {
                    _logger.LogDebug("No timezone configured for TenantId: {TenantId}. Using UTC.", tenantId);
                    timeZone = TimeZoneInfo.Utc;
                }

                // Convert UTC time to tenant's timezone
                var offset = timeZone.GetUtcOffset(utcTime);
                var localTime = new DateTimeOffset(utcTime, TimeSpan.Zero).ToOffset(offset);

                return (localTime, timeZoneId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenant local time for TenantId: {TenantId}. Returning UTC.", tenantId);
                
                var utcTime = utcDateTime ?? DateTime.UtcNow;
                if (utcTime.Kind != DateTimeKind.Utc)
                {
                    utcTime = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
                }
                
                return (new DateTimeOffset(utcTime, TimeSpan.Zero), "UTC");
            }
        }
    }
}
