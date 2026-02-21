using Loyalty.Business.Models.TenantIntegration;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class WalletWebhookService : IWalletWebhookService
    {
        private readonly ILoyaltyCardRepository _loyaltyCardRepo;
        private readonly ILoyaltyProgramRepository _loyaltyProgramRepo;
        private readonly IRewardRepository _rewardRepo;
        private readonly IPosProductEarnRuleRepository _earnRuleRepo;
        private readonly IPosCustomerMappingRepository _customerMappingRepo;
        private readonly IRedeemService _redeemService;
        private readonly IUnitOfWork _uow;
        private readonly LoyaltyWalletDbContext _dbContext;

        public WalletWebhookService(
            ILoyaltyCardRepository loyaltyCardRepo,
            ILoyaltyProgramRepository loyaltyProgramRepo,
            IRewardRepository rewardRepo,
            IPosProductEarnRuleRepository earnRuleRepo,
            IPosCustomerMappingRepository customerMappingRepo,
            IRedeemService redeemService,
            IUnitOfWork uow,
            LoyaltyWalletDbContext dbContext)
        {
            _loyaltyCardRepo = loyaltyCardRepo;
            _loyaltyProgramRepo = loyaltyProgramRepo;
            _rewardRepo = rewardRepo;
            _earnRuleRepo = earnRuleRepo;
            _customerMappingRepo = customerMappingRepo;
            _redeemService = redeemService;
            _uow = uow;
            _dbContext = dbContext;
        }

        public async Task<GetWalletResponse> GetWalletAsync(WalletWebhookResolvedContext context, string barcode, CancellationToken ct = default)
        {
            var tenantId = context.TenantId;
            var programId = context.LoyaltyProgramId;

            var card = await _loyaltyCardRepo.GetByBarcodeValueAsync(tenantId, barcode, ct);
            if (card == null)
                return new GetWalletResponse { Success = false, Message = "Customer not found" };

            if (!programId.HasValue)
                return new GetWalletResponse { Success = false, Message = "No loyalty program configured" };

            var program = await _loyaltyProgramRepo.GetByIdAsync(programId.Value, ct);
            if (program == null || program.Id != programId)
                return new GetWalletResponse { Success = false, Message = "Program not found" };

            var cardInProgram = card.LoyaltyProgramId == programId.Value ? card : null;
            if (cardInProgram == null)
            {
                var cardForProgram = await _loyaltyCardRepo.GetLastActiveByCustomerAsync(tenantId, programId.Value, card.CustomerId, ct);
                cardInProgram = cardForProgram;
            }

            var loyalty = new List<LoyaltyCampaignItem>();
            if (cardInProgram != null)
            {
                CardTemplate? template = null;
                if (program.CardTemplateId.HasValue)
                {
                    template = await _dbContext.Set<CardTemplate>().FindAsync(new object[] { program.CardTemplateId.Value }, ct);
                }
                var totalSteps = template?.TotalSteps ?? 10;
                var rewardsEnum = await _rewardRepo.GetAllAsync(r => r.LoyaltyProgramId == programId.Value && r.IsActive);
                var firstReward = rewardsEnum.FirstOrDefault();
                loyalty.Add(new LoyaltyCampaignItem
                {
                    Id = program.Id,
                    Count = cardInProgram.CurrentSteps,
                    Maximum = totalSteps,
                    Rewards = cardInProgram.CompletedCycles,
                    ProductId = firstReward?.ExternalProductId ?? program.Id.ToString(),
                    ProductName = firstReward?.Name ?? template?.RewardDescription
                });
            }

            var cashbackBalans = (double)(card.CurrentPoints * program.CurrencyPerPoint);

            return new GetWalletResponse
            {
                Success = true,
                Loyalty = loyalty,
                CashbackBalans = cashbackBalans
            };
        }

        public async Task<SetRedeemResponse> SetRedeemAsync(WalletWebhookResolvedContext context, SetRedeemRequest request, CancellationToken ct = default)
        {
            var tenantId = context.TenantId;
            var programId = context.LoyaltyProgramId;
            if (!programId.HasValue)
                return new SetRedeemResponse { Status = false, Message = "No program configured" };

            var card = await _loyaltyCardRepo.GetByBarcodeValueAsync(tenantId, request.Barcode, ct);
            if (card == null)
                return new SetRedeemResponse { Status = false, Message = "Customer not found" };

            var cardForProgram = card.LoyaltyProgramId == programId ? card : await _loyaltyCardRepo.GetLastActiveByCustomerAsync(tenantId, programId.Value, card.CustomerId, ct);
            if (cardForProgram == null)
                return new SetRedeemResponse { Status = false, Message = "No active card for program" };

            var dto = new Dtos.Post.Main.RedeemRequestDto
            {
                TenantId = tenantId,
                LoyaltyProgramId = programId,
                Code = request.Barcode,
                Delta = -1,
                Mode = Dtos.Post.Main.RedeemMode.ForcePending,
                OrderId = null,
                PreferredPlatform = WalletPlatform.Unknown
            };

            var result = await _redeemService.RedeemAsync(dto, ct);
            if (!result.Success)
                return new SetRedeemResponse { Status = false, Message = result.Message ?? "Failed" };

            return new SetRedeemResponse { Status = true, Message = "OTP sent" };
        }

        public async Task<SetVerifyResponse> SetVerifyAsync(WalletWebhookResolvedContext context, SetVerifyRequest request, CancellationToken ct = default)
        {
            var tenantId = context.TenantId;
            var programId = context.LoyaltyProgramId;

            var card = await _loyaltyCardRepo.GetByBarcodeValueAsync(tenantId, request.Barcode, ct);
            if (card == null)
                return new SetVerifyResponse { Status = false };

            var cardForProgram = card.LoyaltyProgramId == programId ? card : await _loyaltyCardRepo.GetLastActiveByCustomerAsync(tenantId, programId ?? 0, card.CustomerId, ct);
            if (cardForProgram == null)
                return new SetVerifyResponse { Status = false };

            var pendingRequest = await _dbContext.Set<RedeemRequest>()
                .Where(rr => rr.LoyaltyCardId == cardForProgram.Id && rr.Status == RedeemStatus.Pending && rr.ExpiresUtc > DateTime.UtcNow)
                .OrderByDescending(rr => rr.Id)
                .FirstOrDefaultAsync(ct);
            if (pendingRequest == null)
                return new SetVerifyResponse { Status = false };

            var result = await _redeemService.ConfirmRedeemAsync(pendingRequest.Id, request.Otp, ct);
            if (!result.Success)
                return new SetVerifyResponse { Status = false };

            if (request.Type == "cashback")
            {
                var program = await _loyaltyProgramRepo.GetByIdAsync(programId ?? 0, ct);
                var pointsRedeemed = (result.OldPoints ?? 0) - (result.NewPoints ?? 0);
                if (pointsRedeemed < 0) pointsRedeemed = 0;
                var discount = request.Amount > 0 ? request.Amount : (decimal)(pointsRedeemed * (double)(program?.CurrencyPerPoint ?? 0.1m));
                return new SetVerifyResponse { Status = true, Type = "cashback", Discount = discount };
            }

                var rewardsEnum2 = await _rewardRepo.GetAllAsync(r => r.LoyaltyProgramId == programId.Value && r.IsActive);
                var firstReward = rewardsEnum2.FirstOrDefault();
            return new SetVerifyResponse
            {
                Status = true,
                Type = "loyalty",
                ProductId = firstReward?.ExternalProductId ?? programId?.ToString(),
                ProductName = firstReward?.Name
            };
        }

        public async Task<CollectResponse> CollectAsync(WalletWebhookResolvedContext context, CollectRequest request, CancellationToken ct = default)
        {
            var tenantIntegrationId = context.TenantIntegrationId;
            var programId = context.LoyaltyProgramId;
            if (!programId.HasValue)
                return new CollectResponse { Success = false };

            var customerId = await _customerMappingRepo.GetCustomerIdByExternalIdAsync(tenantIntegrationId, request.CustomerId, ct);
            if (!customerId.HasValue)
                return new CollectResponse { Success = false };

            var card = await _loyaltyCardRepo.GetLastActiveByCustomerAsync(context.TenantId, programId.Value, customerId.Value, ct);
            if (card == null)
                return new CollectResponse { Success = false };

            if (request.Status == "deleted")
            {
                var txns = await _dbContext.Set<Transaction>()
                    .Where(t => t.OrderId == request.Id && t.LoyaltyCardId == card.Id)
                    .ToListAsync(ct);
                foreach (var tx in txns)
                {
                    var cardRepo = _uow.Repository<LoyaltyCard>();
                    var c = await cardRepo.GetByIdAsync(card.Id, ct);
                    if (c != null)
                    {
                        c.CurrentSteps = Math.Max(0, c.CurrentSteps - tx.Points);
                        c.CurrentPoints = Math.Max(0, c.CurrentPoints - tx.Points);
                        _uow.Repository<LoyaltyCard>().Update(c);
                    }
                    var reverseTx = new Transaction
                    {
                        LoyaltyCardId = card.Id,
                        Type = TransactionType.Adjusted,
                        Points = -tx.Points,
                        OrderId = request.Id,
                        Description = "POS order deleted",
                        RegUserId = 1,
                        RegDate = DateTime.UtcNow,
                        IsActive = true,
                        BalanceAfter = 0,
                        LocalOccurredAt = DateTimeOffset.UtcNow,
                        TimeZoneIdUsed = "UTC"
                    };
                    _uow.Repository<Transaction>().Add(reverseTx);
                }
                _uow.Commit();
                return new CollectResponse { Success = true };
            }

            var earnRules = await _earnRuleRepo.GetByTenantIntegrationIdAsync(tenantIntegrationId, ct);
            var rulesByProduct = earnRules.ToDictionary(r => r.ExternalProductId.Trim(), StringComparer.OrdinalIgnoreCase);

            int totalSteps = 0, totalPoints = 0;
            foreach (var item in request.Items)
            {
                var productIdStr = item.Id.ToString().Trim();
                if (rulesByProduct.TryGetValue(productIdStr, out var rule))
                {
                    totalSteps += rule.StepsPerUnit * item.Count;
                    totalPoints += rule.PointsPerUnit * item.Count;
                }
            }

            if (totalSteps == 0 && totalPoints == 0)
            {
                var program = await _loyaltyProgramRepo.GetByIdAsync(programId.Value, ct);
                if (program != null && program.PointsPerCurrency > 0)
                {
                    totalPoints = (int)((double)request.Total * (double)program.PointsPerCurrency);
                }
            }

            if (totalSteps > 0 || totalPoints > 0)
            {
                var cardRepo = _uow.Repository<LoyaltyCard>();
                var c = await cardRepo.GetByIdAsync(card.Id, ct);
                if (c != null)
                {
                    c.CurrentSteps += totalSteps;
                    c.CurrentPoints += totalPoints;
                    c.LifetimePoints += totalPoints;
                    _uow.Repository<LoyaltyCard>().Update(c);

                    var tx = new Transaction
                    {
                        LoyaltyCardId = card.Id,
                        Type = TransactionType.Earned,
                        Points = totalSteps > 0 ? totalSteps : totalPoints,
                        OrderId = request.Id,
                        Description = "POS collect",
                        RegUserId = 1,
                        RegDate = DateTime.UtcNow,
                        IsActive = true,
                        BalanceAfter = c.CurrentPoints,
                        LocalOccurredAt = DateTimeOffset.UtcNow,
                        TimeZoneIdUsed = "UTC"
                    };
                    _uow.Repository<Transaction>().Add(tx);
                    _uow.Commit();
                }
            }

            return new CollectResponse { Success = true };
        }
    }
}
