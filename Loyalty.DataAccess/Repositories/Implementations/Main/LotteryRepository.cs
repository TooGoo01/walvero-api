using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class LotteryRepository : GenericRepository<LotteryTicket>, ILotteryRepository
    {
        private readonly LoyaltyWalletDbContext _context;

        public LotteryRepository(LoyaltyWalletDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IEnumerable<LotteryTicket>> GetTicketsByCardIdAsync(int cardId)
        {
            return await GetAsQueryable()
                .Where(t => t.LoyaltyCardId == cardId)
                .OrderByDescending(t => t.RegDate)
                .ToListAsync();
        }

        public async Task<int> GetActiveTicketCountByCardIdAsync(int cardId)
        {
            return await GetAsQueryable()
                .CountAsync(t => t.LoyaltyCardId == cardId && t.Status == LotteryTicketStatus.Active);
        }

        public async Task<IEnumerable<LotteryTicket>> GetActiveTicketsByTenantAndProgramAsync(int tenantId, int programId)
        {
            return await GetAsQueryable()
                .Include(t => t.LoyaltyCard)
                    .ThenInclude(c => c.Customer)
                .Where(t => t.TenantId == tenantId
                    && t.Status == LotteryTicketStatus.Active
                    && t.LoyaltyCard.LoyaltyProgramId == programId)
                .ToListAsync();
        }

        public async Task<IEnumerable<LotteryTicket>> GetTicketsByTenantAndProgramAsync(int tenantId, int programId)
        {
            return await GetAsQueryable()
                .Include(t => t.LoyaltyCard)
                    .ThenInclude(c => c.Customer)
                .Where(t => t.TenantId == tenantId
                    && t.LoyaltyCard.LoyaltyProgramId == programId)
                .OrderByDescending(t => t.RegDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<LotteryDraw>> GetDrawsByTenantAndProgramAsync(int tenantId, int programId)
        {
            return await _context.LotteryDraws
                .Include(d => d.WinningTickets)
                    .ThenInclude(t => t.LoyaltyCard)
                        .ThenInclude(c => c.Customer)
                .Where(d => d.TenantId == tenantId && d.LoyaltyProgramId == programId)
                .OrderByDescending(d => d.RegDate)
                .ToListAsync();
        }

        public async Task<LotteryDraw?> GetDrawByIdAsync(int drawId)
        {
            return await _context.LotteryDraws
                .Include(d => d.WinningTickets)
                    .ThenInclude(t => t.LoyaltyCard)
                        .ThenInclude(c => c.Customer)
                .FirstOrDefaultAsync(d => d.Id == drawId);
        }

        public async Task<IEnumerable<LotteryTicket>> GetWinsByCardIdAsync(int cardId)
        {
            return await GetAsQueryable()
                .Include(t => t.LotteryDraw)
                .Where(t => t.LoyaltyCardId == cardId && t.Status == LotteryTicketStatus.Won)
                .OrderByDescending(t => t.RegDate)
                .ToListAsync();
        }

        public async Task<LotteryDraw?> GetNextScheduledDrawAsync(int tenantId, int programId)
        {
            return await _context.LotteryDraws
                .Where(d => d.TenantId == tenantId
                    && d.LoyaltyProgramId == programId
                    && d.Status == LotteryDrawStatus.Pending
                    && d.ScheduledAt != null)
                .OrderBy(d => d.ScheduledAt)
                .FirstOrDefaultAsync();
        }
    }
}
