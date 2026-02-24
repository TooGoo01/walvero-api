using Loyalty.Business.Models.TenantIntegration;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class WalletWebhookService : IWalletWebhookService
    {
        private readonly ILoyaltyCardRepository _loyaltyCardRepo;
        private readonly ILoyaltyProgramRepository _loyaltyProgramRepo;
        private readonly IRewardRepository _rewardRepo;
        private readonly ICardTemplateRepository _cardTemplateRepo;
        private readonly IPosProductEarnRuleRepository _earnRuleRepo;
        private readonly IPosCustomerMappingRepository _customerMappingRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly IRedeemService _redeemService;
        private readonly IUnitOfWork _uow;

        public WalletWebhookService(
            ILoyaltyCardRepository loyaltyCardRepo,
            ILoyaltyProgramRepository loyaltyProgramRepo,
            IRewardRepository rewardRepo,
            ICardTemplateRepository cardTemplateRepo,
            IPosProductEarnRuleRepository earnRuleRepo,
            IPosCustomerMappingRepository customerMappingRepo,
            ICustomerRepository customerRepo,
            IRedeemService redeemService,
            IUnitOfWork uow)
        {
            _loyaltyCardRepo = loyaltyCardRepo;
            _loyaltyProgramRepo = loyaltyProgramRepo;
            _rewardRepo = rewardRepo;
            _cardTemplateRepo = cardTemplateRepo;
            _earnRuleRepo = earnRuleRepo;
            _customerMappingRepo = customerMappingRepo;
            _customerRepo = customerRepo;
            _redeemService = redeemService;
            _uow = uow;
        }

        /// <summary>
        /// Barcode "walveroId,barcode" formatında gələ bilər.
        /// Vergüllə ayrılıbsa: əvvəlcə birinci dəyəri, tapılmazsa ikincini axtarır.
        /// Hər dəyər üçün: BarcodeValue (kart barkodu) → WalveroId (ApplicationUser) sırası ilə yoxlanılır.
        /// </summary>
        private async Task<LoyaltyCard?> ResolveCardAsync(int tenantId, string rawBarcode, CancellationToken ct)
        {
            var parts = rawBarcode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var part in parts)
            {
                // 1) BarcodeValue ilə axtar (tracked + Customer include)
                var card = await _loyaltyCardRepo.GetByBarcodeValueAsync(tenantId, part, ct);
                if (card != null) return card;

                // 2) WalveroId ilə axtar (ApplicationUser.WalveroId)
                if (long.TryParse(part, out var walveroId))
                {
                    var customer = await _customerRepo.GetByWalveroIdAsync(tenantId, walveroId, ct);
                    if (customer != null)
                    {
                        // Customer-in son aktiv kartını qaytar
                        var activeCard = customer.LoyaltyCards?
                            .Where(c => c.IsActive)
                            .OrderByDescending(c => c.Id)
                            .FirstOrDefault();
                        if (activeCard != null)
                            return await _loyaltyCardRepo.GetById(activeCard.Id, ct);
                    }
                }
            }

            return null;
        }

        public async Task<GetWalletResponse> GetWalletAsync(WalletWebhookResolvedContext context, string barcode, CancellationToken ct = default)
        {
            var tenantId = context.TenantId;
            var programId = context.LoyaltyProgramId;

            var card = await ResolveCardAsync(tenantId, barcode, ct);
            if (card == null)
                return new GetWalletResponse { Success = false, Message = "Customer not found" };

            if (!programId.HasValue)
                return new GetWalletResponse { Success = false, Message = "No loyalty program configured" };

            var program = await _loyaltyProgramRepo.GetByIdAsync(programId.Value, ct);
            if (program == null)
                return new GetWalletResponse { Success = false, Message = "Program not found" };

            var cardInProgram = card.LoyaltyProgramId == programId.Value ? card : null;
            if (cardInProgram == null)
            {
                cardInProgram = await _loyaltyCardRepo.GetLastActiveByCustomerAsync(tenantId, programId.Value, card.CustomerId, ct);
            }

            var loyalty = new List<LoyaltyCampaignItem>();
            double cashbackBalans = 0;

            if (cardInProgram != null)
            {
                cashbackBalans = (double)((cardInProgram.CurrentPoints) * program.CurrencyPerPoint);

                // Yalnız ProgressBased (stamp) proqramlarda loyalty kampaniyalarını qaytar
                if (program.ProgramType == LoyaltyProgramType.ProgressBased)
                {
                    CardTemplate? template = null;
                    if (program.CardTemplateId.HasValue)
                    {
                        template = await _cardTemplateRepo.GetByIdAsync(program.CardTemplateId.Value, ct);
                    }
                    var totalSteps = template?.TotalSteps ?? 10;
                    var rewards = (await _rewardRepo.GetAllAsync(r => r.LoyaltyProgramId == programId.Value && r.IsActive)).ToList();

                    if (rewards.Count == 0)
                    {
                        loyalty.Add(new LoyaltyCampaignItem
                        {
                            Id = program.Id,
                            Count = cardInProgram.CurrentSteps,
                            Maximum = totalSteps,
                            Rewards = cardInProgram.CompletedCycles,
                            ProductId = program.Id.ToString(),
                            ProductName = template?.RewardDescription
                        });
                    }
                    else
                    {
                        foreach (var reward in rewards)
                        {
                            loyalty.Add(new LoyaltyCampaignItem
                            {
                                Id = reward.Id,
                                Count = cardInProgram.CurrentSteps,
                                Maximum = totalSteps,
                                Rewards = cardInProgram.CompletedCycles,
                                ProductId = reward.ExternalProductId ?? reward.Id.ToString(),
                                ProductName = reward.Name
                            });
                        }
                    }
                }
                // TierBased və PointsBased — yalnız cashback_balans qaytarılır, loyalty boş olur
            }

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

            var card = await ResolveCardAsync(tenantId, request.Barcode, ct);
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

            if (!programId.HasValue)
                return new SetVerifyResponse { Status = false };

            var card = await ResolveCardAsync(tenantId, request.Barcode, ct);
            if (card == null)
                return new SetVerifyResponse { Status = false };

            var cardForProgram = card.LoyaltyProgramId == programId ? card : await _loyaltyCardRepo.GetLastActiveByCustomerAsync(tenantId, programId.Value, card.CustomerId, ct);
            if (cardForProgram == null)
                return new SetVerifyResponse { Status = false };

            var allPending = await _uow.Repository<RedeemRequest>().GetAllAsync(
                rr => rr.LoyaltyCardId == cardForProgram.Id && rr.Status == RedeemStatus.Pending && rr.ExpiresUtc > DateTime.UtcNow);
            var pendingRequest = allPending.OrderByDescending(rr => rr.Id).FirstOrDefault();
            if (pendingRequest == null)
                return new SetVerifyResponse { Status = false };

            var result = await _redeemService.ConfirmRedeemAsync(pendingRequest.Id, request.Otp, ct);
            if (!result.Success)
                return new SetVerifyResponse { Status = false };

            if (request.Type == "cashback")
            {
                var program = await _loyaltyProgramRepo.GetByIdAsync(programId.Value, ct);
                var pointsRedeemed = (result.OldPoints ?? 0) - (result.NewPoints ?? 0);
                if (pointsRedeemed < 0) pointsRedeemed = 0;
                var discount = request.Amount > 0 ? request.Amount : (decimal)(pointsRedeemed * (double)(program?.CurrencyPerPoint ?? 0.1m));
                return new SetVerifyResponse { Status = true, Type = "cashback", Discount = discount };
            }

            // campaign_id ilə göndərilən reward-u tap; tapılmazsa birincini götür
            Reward? matchedReward = null;
            if (int.TryParse(request.CampaignId, out var campaignRewardId))
            {
                var rewardById = await _rewardRepo.GetByIdAsync(campaignRewardId, ct);
                if (rewardById != null && rewardById.IsActive && rewardById.LoyaltyProgramId == programId.Value)
                    matchedReward = rewardById;
            }
            matchedReward ??= (await _rewardRepo.GetAllAsync(r => r.LoyaltyProgramId == programId.Value && r.IsActive)).FirstOrDefault();

            return new SetVerifyResponse
            {
                Status = true,
                Type = "loyalty",
                ProductId = matchedReward?.ExternalProductId ?? programId.Value.ToString(),
                ProductName = matchedReward?.Name
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
                var txns = (await _uow.Repository<Transaction>().GetAllAsync(
                    t => t.OrderId == request.Id && t.LoyaltyCardId == card.Id)).ToList();

                foreach (var tx in txns)
                {
                    var c = await _uow.Repository<LoyaltyCard>().GetByIdAsync(card.Id, ct);
                    if (c != null)
                    {
                        c.CurrentSteps = Math.Max(0, c.CurrentSteps - tx.Points);
                        c.CurrentPoints = Math.Max(0, c.CurrentPoints - tx.Points);
                        _uow.Repository<LoyaltyCard>().Update(c);

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
                            BalanceAfter = c.CurrentPoints,
                            LocalOccurredAt = DateTimeOffset.UtcNow,
                            TimeZoneIdUsed = "UTC"
                        };
                        _uow.Repository<Transaction>().Add(reverseTx);
                    }
                }
                await _uow.CommitAsync(ct);
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
                var c = await _uow.Repository<LoyaltyCard>().GetByIdAsync(card.Id, ct);
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
                    await _uow.CommitAsync(ct);
                }
            }

            return new CollectResponse { Success = true };
        }
    }
}
