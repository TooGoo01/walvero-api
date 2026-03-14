using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ILotteryRepository : IGenericRepository<LotteryTicket>
    {
        Task<IEnumerable<LotteryTicket>> GetTicketsByCardIdAsync(int cardId);
        Task<int> GetActiveTicketCountByCardIdAsync(int cardId);
        Task<IEnumerable<LotteryTicket>> GetActiveTicketsByTenantAndProgramAsync(int tenantId, int programId);
        Task<IEnumerable<LotteryTicket>> GetTicketsByTenantAndProgramAsync(int tenantId, int programId);
        Task<IEnumerable<LotteryDraw>> GetDrawsByTenantAndProgramAsync(int tenantId, int programId);
        Task<LotteryDraw?> GetDrawByIdAsync(int drawId);
        Task<IEnumerable<LotteryTicket>> GetWinsByCardIdAsync(int cardId);
        Task<LotteryDraw?> GetNextScheduledDrawAsync(int tenantId, int programId);
    }
}
