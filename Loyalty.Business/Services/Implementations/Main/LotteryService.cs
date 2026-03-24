using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class LotteryService : ILotteryService
    {
        private readonly ILotteryRepository _lotteryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService? _notificationService;
        private readonly ILogger<LotteryService>? _logger;

        private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public LotteryService(
            ILotteryRepository lotteryRepository,
            IUnitOfWork unitOfWork,
            INotificationService? notificationService = null,
            ILogger<LotteryService>? logger = null)
        {
            _lotteryRepository = lotteryRepository;
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _logger = logger;
        }

        public string GenerateTicketCode()
        {
            var bytes = RandomNumberGenerator.GetBytes(8);
            var code = string.Concat(bytes.Select(b => CodeChars[b % CodeChars.Length]));
            return $"WLV-{code}";
        }

        public async Task<ServiceResult> GetTicketsByCardAsync(int cardId)
        {
            var tickets = await _lotteryRepository.GetTicketsByCardIdAsync(cardId);
            var dtos = tickets.Select(t => MapTicketDto(t)).ToList();
            return new ServiceResult(true, dtos);
        }

        public async Task<ServiceResult> GetTicketCountByCardAsync(int cardId)
        {
            var count = await _lotteryRepository.GetActiveTicketCountByCardIdAsync(cardId);
            return new ServiceResult(true, count);
        }

        public async Task<ServiceResult> GetTicketsByProgramAsync(int tenantId, int programId)
        {
            var tickets = await _lotteryRepository.GetTicketsByTenantAndProgramAsync(tenantId, programId);
            var dtos = tickets.Select(t => MapTicketDto(t)).ToList();
            return new ServiceResult(true, dtos);
        }

        public async Task<ServiceResult> GetDrawsAsync(int tenantId, int programId)
        {
            var draws = await _lotteryRepository.GetDrawsByTenantAndProgramAsync(tenantId, programId);
            var dtos = draws.Select(d => MapDrawDto(d)).ToList();
            return new ServiceResult(true, dtos);
        }

        public async Task<ServiceResult> CreateDrawAsync(CreateLotteryDrawDto dto, int tenantId)
        {
            var draw = new LotteryDraw
            {
                TenantId = tenantId,
                LoyaltyProgramId = dto.LoyaltyProgramId,
                DrawName = dto.DrawName,
                PrizeName = dto.PrizeName,
                PrizeDescription = dto.PrizeDescription,
                WinnerCount = dto.WinnerCount,
                ScheduledAt = dto.ScheduledAt,
                Status = LotteryDrawStatus.Pending
            };

            await _unitOfWork.Repository<LotteryDraw>().AddAsync(draw);
            _unitOfWork.Commit();

            return new ServiceResult(true, MapDrawDto(draw));
        }

        public async Task<ServiceResult> ExecuteDrawAsync(int drawId)
        {
            var draw = await _lotteryRepository.GetDrawByIdAsync(drawId);
            if (draw == null)
                return new ServiceResult(false, "Çəkiliş tapılmadı.");

            if (draw.Status != LotteryDrawStatus.Pending)
                return new ServiceResult(false, "Bu çəkiliş artıq tamamlanıb və ya ləğv edilib.");

            // Get all active tickets for this program & tenant
            var activeTickets = (await _lotteryRepository
                .GetActiveTicketsByTenantAndProgramAsync(draw.TenantId, draw.LoyaltyProgramId))
                .ToList();

            if (activeTickets.Count == 0)
                return new ServiceResult(false, "Aktiv bilet tapılmadı.");

            var winnerCount = Math.Min(draw.WinnerCount, activeTickets.Count);

            // Shuffle using Fisher-Yates with cryptographic randomness
            for (int i = activeTickets.Count - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (activeTickets[i], activeTickets[j]) = (activeTickets[j], activeTickets[i]);
            }

            // Pick winners
            var winners = activeTickets.Take(winnerCount).ToList();
            var losers = activeTickets.Skip(winnerCount).ToList();

            // Mark winners
            foreach (var ticket in winners)
            {
                ticket.Status = LotteryTicketStatus.Won;
                ticket.LotteryDrawId = draw.Id;
                _unitOfWork.Repository<LotteryTicket>().Update(ticket);
            }

            // Mark all remaining as Used (consumed after draw)
            foreach (var ticket in losers)
            {
                ticket.Status = LotteryTicketStatus.Used;
                ticket.LotteryDrawId = draw.Id;
                _unitOfWork.Repository<LotteryTicket>().Update(ticket);
            }

            draw.Status = LotteryDrawStatus.Completed;
            draw.DrawnAt = DateTime.UtcNow;
            _unitOfWork.Repository<LotteryDraw>().Update(draw);
            _unitOfWork.Commit();

            // Notify winners via push notification
            if (_notificationService != null)
            {
                foreach (var ticket in winners)
                {
                    try
                    {
                        var userId = ticket.LoyaltyCard?.Customer?.applicationUserId;
                        if (userId.HasValue)
                        {
                            await _notificationService.SendToUserAsync(
                                userId.Value,
                                "Təbriklər! Çəkilişdə qalib oldunuz!",
                                $"Siz '{draw.DrawName}' çəkilişində '{draw.PrizeName}' mükafatını qazandınız!",
                                new Dictionary<string, string>
                                {
                                    { "type", "lottery_win" },
                                    { "drawId", draw.Id.ToString() },
                                    { "prize", draw.PrizeName ?? "" }
                                });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to notify lottery winner for ticket {TicketId}", ticket.Id);
                    }
                }
            }

            // Reload draw with winners
            var updatedDraw = await _lotteryRepository.GetDrawByIdAsync(drawId);
            if (updatedDraw == null)
                return new ServiceResult(true, "Çəkiliş tamamlandı, amma yenidən yüklənə bilmədi.");

            return new ServiceResult(true, MapDrawDto(updatedDraw));
        }

        public async Task<ServiceResult> GetWinsByCardAsync(int cardId)
        {
            var wins = await _lotteryRepository.GetWinsByCardIdAsync(cardId);
            var dtos = wins.Select(t => MapTicketDto(t)).ToList();
            return new ServiceResult(true, dtos);
        }

        private static GetLotteryTicketDto MapTicketDto(LotteryTicket t) => new()
        {
            Id = t.Id,
            TicketCode = t.TicketCode,
            Status = t.Status,
            LoyaltyCardId = t.LoyaltyCardId,
            CustomerName = t.LoyaltyCard?.Customer != null
                ? $"{t.LoyaltyCard.Customer.FirstName} {t.LoyaltyCard.Customer.LastName}".Trim()
                : null,
            CardNumber = t.LoyaltyCard?.CardNumber,
            LotteryDrawId = t.LotteryDrawId,
            CreatedAt = t.RegDate
        };

        private static GetLotteryDrawDto MapDrawDto(LotteryDraw d) => new()
        {
            Id = d.Id,
            DrawName = d.DrawName,
            PrizeName = d.PrizeName,
            PrizeDescription = d.PrizeDescription,
            WinnerCount = d.WinnerCount,
            Status = d.Status,
            ScheduledAt = d.ScheduledAt,
            DrawnAt = d.DrawnAt,
            LoyaltyProgramId = d.LoyaltyProgramId,
            TenantId = d.TenantId,
            CreatedAt = d.RegDate,
            Winners = d.WinningTickets?
                .Where(t => t.Status == LotteryTicketStatus.Won)
                .Select(t => MapTicketDto(t))
                .ToList() ?? new(),
            TotalTickets = d.WinningTickets?.Count ?? 0  // WinningTickets = all tickets assigned to this draw (winners + losers)
        };
    }
}
