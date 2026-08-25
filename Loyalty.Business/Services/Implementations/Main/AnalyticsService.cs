using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly LoyaltyWalletDbContext _dbContext;

        public AnalyticsService(LoyaltyWalletDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ServiceResult> TrackLinkClickAsync(TrackLinkClickDto dto)
        {
            if (dto.TenantId <= 0 || string.IsNullOrWhiteSpace(dto.LinkType))
                return new ServiceResult(false, "TenantId və LinkType tələb olunur");

            var linkType = dto.LinkType.Trim().ToLowerInvariant();
            if (linkType.Length > 50) linkType = linkType.Substring(0, 50);

            var evt = new LinkClickEvent
            {
                TenantId = dto.TenantId,
                BranchId = dto.BranchId,
                LinkType = linkType,
                ClickedAt = DateTime.UtcNow,
                RegDate = DateTime.UtcNow,
            };

            _dbContext.LinkClickEvents.Add(evt);
            await _dbContext.SaveChangesAsync();

            return new ServiceResult(true);
        }

        public async Task<ServiceResult<LinkClickStatsDto>> GetLinkClickStatsAsync(
            int tenantId,
            DateOnly startDate,
            DateOnly endDate)
        {
            var start = startDate.ToDateTime(TimeOnly.MinValue);
            // endDate daxil olsun deyə növbəti günün başlanğıcına qədər
            var endExclusive = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

            var events = await _dbContext.LinkClickEvents
                .Where(e => e.TenantId == tenantId
                    && e.ClickedAt >= start
                    && e.ClickedAt < endExclusive)
                .Select(e => new { e.LinkType, e.ClickedAt })
                .ToListAsync();

            var byLinkType = events
                .GroupBy(e => e.LinkType)
                .Select(g => new LinkTypeCountDto { LinkType = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            var series = events
                .GroupBy(e => DateOnly.FromDateTime(e.ClickedAt))
                .Select(g => new DailyClickCountDto { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToList();

            var dto = new LinkClickStatsDto
            {
                Total = events.Count,
                ByLinkType = byLinkType,
                Series = series,
            };

            return ServiceResult<LinkClickStatsDto>.Ok(dto);
        }
    }
}
