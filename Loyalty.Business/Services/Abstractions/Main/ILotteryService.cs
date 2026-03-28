using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ILotteryService
    {
        Task<ServiceResult> GetTicketsByCardAsync(int cardId);
        Task<ServiceResult> GetTicketCountByCardAsync(int cardId);
        Task<ServiceResult> GetTicketsByProgramAsync(int tenantId, int programId);
        Task<ServiceResult> GetDrawsAsync(int tenantId, int programId);
        Task<ServiceResult> CreateDrawAsync(CreateLotteryDrawDto dto, int tenantId);
        Task<ServiceResult> ExecuteDrawAsync(int drawId);
        Task<ServiceResult> GetWinsByCardAsync(int cardId);
        Task<ServiceResult> DeletePrizeAsync(int prizeId);
        Task<ServiceResult> DeleteDrawAsync(int drawId);
        string GenerateTicketCode();
    }
}
