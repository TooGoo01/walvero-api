using Loyalty.Business.Dtos.Chatbot;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class CustomerAssistantTools : ICustomerAssistantTools
    {
        private readonly ITenantProvider _tenantProvider;
        private readonly ILoyaltyCardRepository _cardRepository;
        private readonly ITransactionService _transactionService;

        public CustomerAssistantTools(
            ITenantProvider tenantProvider,
            ILoyaltyCardRepository cardRepository,
            ITransactionService transactionService)
        {
            _tenantProvider = tenantProvider;
            _cardRepository = cardRepository;
            _transactionService = transactionService;
        }

        public bool IsAuthenticated =>
            int.TryParse(_tenantProvider.CurrentUserId, out var id) && id > 0;

        private int? UserId =>
            int.TryParse(_tenantProvider.CurrentUserId, out var id) && id > 0 ? id : (int?)null;

        public async Task<List<MyCardDto>> GetMyCardsAsync(string? placeName, CancellationToken ct = default)
        {
            var uid = UserId;
            if (uid is null) return new List<MyCardDto>();

            var cards = await _cardRepository.GetLastActiveCardsForApplicationUserAsync(
                uid.Value, string.IsNullOrWhiteSpace(placeName) ? null : placeName, null, ct);

            return cards.Select(c => new MyCardDto
            {
                TenantId = c.TenantId,
                TenantName = c.Tenant?.CompanyName ?? string.Empty,
                ProgramName = c.LoyaltyProgram?.ProgramName ?? string.Empty,
                CurrentPoints = c.CurrentPoints,
                CurrentSteps = c.CurrentSteps,
                CompletedCycles = c.CompletedCycles
            }).ToList();
        }

        public async Task<List<MyTransactionDto>> GetMyTransactionsAsync(string? placeName, int limit, CancellationToken ct = default)
        {
            var uid = UserId;
            if (uid is null) return new List<MyTransactionDto>();

            var result = await _transactionService.GetMyTransactionsAsync(uid.Value, ct);
            if (result?.Data is not IEnumerable<GetTransactionDto> txs)
                return new List<MyTransactionDto>();

            IEnumerable<GetTransactionDto> filtered = txs;
            if (!string.IsNullOrWhiteSpace(placeName))
            {
                filtered = filtered.Where(t =>
                    !string.IsNullOrWhiteSpace(t.TenantName) &&
                    t.TenantName!.Contains(placeName, StringComparison.OrdinalIgnoreCase));
            }

            int take = Math.Clamp(limit <= 0 ? 10 : limit, 1, 30);

            return filtered
                .OrderByDescending(t => t.LocalOccurredAt)
                .Take(take)
                .Select(t => new MyTransactionDto
                {
                    TenantName = t.TenantName ?? string.Empty,
                    BranchName = t.BranchName,
                    Type = t.Type.ToString(),
                    Points = t.Points,
                    Amount = t.Amount,
                    BalanceAfter = t.BalanceAfter,
                    OccurredAt = t.LocalOccurredAt
                })
                .ToList();
        }
    }
}
