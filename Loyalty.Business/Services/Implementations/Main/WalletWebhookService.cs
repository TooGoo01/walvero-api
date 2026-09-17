using Loyalty.Business.Helpers;
using Loyalty.Business.Models.TenantIntegration;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class WalletWebhookService : IWalletWebhookService
    {
        private readonly ILoyaltyCardRepository _loyaltyCardRepo;
        private readonly ILoyaltyProgramRepository _loyaltyProgramRepo;
        private readonly IRewardRepository _rewardRepo;
        private readonly ICardTemplateRepository _cardTemplateRepo;
        private readonly IPosProductEarnRuleRepository _earnRuleRepo;
        private readonly IPosLoyaltyDiscountTypeRepository _loyaltyDiscountTypeRepo;
        private readonly IPosCustomerMappingRepository _customerMappingRepo;
        private readonly ICustomerRepository _customerRepo;
        private readonly IRedeemService _redeemService;
        private readonly ITenantRepository _tenantRepo;
        private readonly IWalletRouter _walletRouter;
        private readonly ITenantBrandingRepository _brandingRepo;
        private readonly ITimeZoneService _timeZoneService;
        private readonly ILogger<WalletWebhookService> _logger;
        private readonly IUnitOfWork _uow;

        public WalletWebhookService(
            ILoyaltyCardRepository loyaltyCardRepo,
            ILoyaltyProgramRepository loyaltyProgramRepo,
            IRewardRepository rewardRepo,
            ICardTemplateRepository cardTemplateRepo,
            IPosProductEarnRuleRepository earnRuleRepo,
            IPosLoyaltyDiscountTypeRepository loyaltyDiscountTypeRepo,
            IPosCustomerMappingRepository customerMappingRepo,
            ICustomerRepository customerRepo,
            IRedeemService redeemService,
            ITenantRepository tenantRepo,
            IWalletRouter walletRouter,
            ITenantBrandingRepository brandingRepo,
            ITimeZoneService timeZoneService,
            ILogger<WalletWebhookService> logger,
            IUnitOfWork uow)
        {
            _loyaltyCardRepo = loyaltyCardRepo;
            _loyaltyProgramRepo = loyaltyProgramRepo;
            _rewardRepo = rewardRepo;
            _cardTemplateRepo = cardTemplateRepo;
            _earnRuleRepo = earnRuleRepo;
            _loyaltyDiscountTypeRepo = loyaltyDiscountTypeRepo;
            _customerMappingRepo = customerMappingRepo;
            _customerRepo = customerRepo;
            _redeemService = redeemService;
            _tenantRepo = tenantRepo;
            _walletRouter = walletRouter;
            _brandingRepo = brandingRepo;
            _timeZoneService = timeZoneService;
            _logger = logger;
            _uow = uow;
        }

        /// <summary>
        /// CardTemplate entity-si üzərindən tier hesablayır (RedeemController.TierResolverDouble kimi).
        /// </summary>
        private static (string tierName, int? percent, int? percentCash, string? iconUrl) ResolveTier(
            decimal points, CardTemplate tpl)
        {
            int bronzeTh = tpl.BronzeThreshold ?? 0;
            int? silverTh = tpl.SilverThreshold;
            int? goldTh = tpl.GoldThreshold;
            int? platTh = tpl.PlatinumThreshold;

            string name = "Bronze";
            int? percent = tpl.BronzePercent;
            int? percentCash = tpl.BronzePercentCash;
            string? icon = tpl.BronzeIconUrl ?? tpl.LogoUrl;

            if (platTh.HasValue && points >= platTh.Value)
            {
                name = "Platinum";
                percent = tpl.PlatinumPercent;
                percentCash = tpl.PlatinumPercentCash;
                icon = tpl.PlatinumIconUrl ?? icon;
            }
            else if (goldTh.HasValue && points >= goldTh.Value)
            {
                name = "Gold";
                percent = tpl.GoldPercent;
                percentCash = tpl.GoldPercentCash;
                icon = tpl.GoldIconUrl ?? icon;
            }
            else if (silverTh.HasValue && points >= silverTh.Value)
            {
                name = "Silver";
                percent = tpl.SilverPercent;
                percentCash = tpl.SilverPercentCash;
                icon = tpl.SilverIconUrl ?? icon;
            }

            return (name, percent, percentCash, icon);
        }

        /// <summary>
        /// Barcode "walveroId,barcode" formatında gələ bilər.
        /// Vergüllə ayrılıbsa: əvvəlcə birinci dəyəri, tapılmazsa ikincini axtarır.
        /// Hər dəyər üçün: BarcodeValue (kart barkodu) → WalveroId (ApplicationUser) sırası ilə yoxlanılır.
        /// </summary>
        private async Task<LoyaltyCard?> ResolveCardAsync(int tenantId, string rawBarcode, int? programId, CancellationToken ct)
        {
            var parts = rawBarcode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var part in parts)
            {
                // 1) BarcodeValue ilə axtar (tracked + Customer include)
                var card = await _loyaltyCardRepo.GetByBarcodeValueAsync(tenantId, part, ct);
                if (card != null)
                {
                    // programId varsa ve uyğun deyilse, skip et
                    if (programId.HasValue && card.LoyaltyProgramId != programId.Value)
                        continue;
                    return card;
                }

                // 2) WalveroId ilə user tap → phone nömrəsi ilə customer-i dəqiqləşdir
                if (long.TryParse(part, out var walveroId))
                {
                    var customerByWalvero = await _customerRepo.GetByWalveroIdAsync(tenantId, walveroId, ct);
                    Customer? customer = customerByWalvero;

                    // User-in phone nömrəsi ilə customer-i dəqiqləşdir
                    var phone = customerByWalvero?.applicationUser?.UserName;
                    if (!string.IsNullOrEmpty(phone))
                    {
                        var phoneCustomer = await _customerRepo.GetByPhoneAsync(tenantId, phone, ct);
                        if (phoneCustomer != null)
                            customer = phoneCustomer;
                    }

                    if (customer != null)
                    {
                        var activeCard = customer.LoyaltyCards?
                            .Where(c => c.IsActive && (!programId.HasValue || c.LoyaltyProgramId == programId.Value))
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

            var card = await ResolveCardAsync(tenantId, barcode, programId, ct);
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
                // ProgressBased üçün cashback_balans göstərilmir
                if (program.ProgramType != LoyaltyProgramType.ProgressBased)
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

                    var productList = rewards.Count > 0
                        ? rewards.Select(r => new ProductItem
                        {
                            ProductId = r.ExternalProductId ?? r.Id.ToString(),
                            ProductName = r.Name
                        }).ToList()
                        : new List<ProductItem>
                        {
                            new ProductItem
                            {
                                ProductId = program.Id.ToString(),
                                ProductName = template?.RewardDescription
                            }
                        };

                    var firstProduct = productList.First();

                    loyalty.Add(new LoyaltyCampaignItem
                    {
                        Id = program.Id,
                        Count = cardInProgram.CompletedCycles * totalSteps + cardInProgram.CurrentSteps,
                        Maximum = totalSteps,
                        Rewards = cardInProgram.CompletedCycles,
                        ProductId = firstProduct.ProductId,
                        ProductName = firstProduct.ProductName,
                        Products = productList
                    });
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

            var card = await ResolveCardAsync(tenantId, request.Barcode, programId, ct);
            if (card == null)
                return new SetRedeemResponse { Status = false, Message = "Customer not found" };

            var cardForProgram = card.LoyaltyProgramId == programId ? card : await _loyaltyCardRepo.GetLastActiveByCustomerAsync(tenantId, programId.Value, card.CustomerId, ct);
            if (cardForProgram == null)
                return new SetRedeemResponse { Status = false, Message = "No active card for program" };

            decimal delta = request.Delta;

            var program = await _loyaltyProgramRepo.GetByIdAsync(programId.Value, ct);
            if (program != null && program.CardTemplateId.HasValue)
            {
                var tpl = await _cardTemplateRepo.GetByIdAsync(program.CardTemplateId.Value, ct);
                if (tpl != null && tpl.TemplateType == CardTemplateType.ProgressBased)
                {
                    // ProgressBased: hər reward = TotalSteps point, pending request yaradılır (steps integer)
                    int totalSteps = Math.Max(1, tpl.TotalSteps);
                    int rewardsToSpend = (int)Math.Max(1, request.Amount);
                    delta = -(rewardsToSpend * totalSteps);
                }
                else if (string.Equals(request.Type, "cashback", StringComparison.OrdinalIgnoreCase)
                         && request.Amount > 0)
                {
                    // Cashback: Amount birbaşa point kimi çıxılır (kəsr saxlanır)
                    delta = -request.Amount;
                }
                else
                {
                    if (delta > 0) delta = -delta;
                }
            }
            else if (string.Equals(request.Type, "cashback", StringComparison.OrdinalIgnoreCase)
                     && request.Amount > 0)
            {
                delta = -request.Amount;
            }
            else
            {
                if (delta > 0) delta = -delta;
            }

            var dto = new Dtos.Post.Main.RedeemRequestDto
            {
                TenantId = tenantId,
                LoyaltyProgramId = programId,
                Code = request.Barcode,
                Delta = delta,
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

            var card = await ResolveCardAsync(tenantId, request.Barcode, programId, ct);
            if (card == null)
                return new SetVerifyResponse { Status = false };

            var cardForProgram = card.LoyaltyProgramId == programId ? card : await _loyaltyCardRepo.GetLastActiveByCustomerAsync(tenantId, programId.Value, card.CustomerId, ct);
            if (cardForProgram == null)
                return new SetVerifyResponse { Status = false };

            var allPending = await _uow.Repository<RedeemRequest>().GetAllAsync(
                rr => rr.LoyaltyCardId == cardForProgram.Id && rr.Status == RedeemStatus.Pending && rr.ExpiresUtc > DateTime.UtcNow);
            var pendingRequest = allPending.OrderByDescending(rr => rr.Id).FirstOrDefault();

            if (pendingRequest == null)
            {
                // No pending request - check if RequireConfirmation is off
                var tenant = await _tenantRepo.GetByIdAsync(tenantId, ct);
                if (tenant == null || tenant.RequireConfirmation)
                    return new SetVerifyResponse { Status = false };

                // RequireConfirmation=false: create redeem inline then confirm
                var inlineRedeem = new SetRedeemRequest
                {
                    VenueId = request.VenueId,
                    Barcode = request.Barcode,
                    CampaignId = request.CampaignId,
                    Type = request.Type,
                    Amount = request.Amount,
                    Delta = -1
                };
                var redeemResp = await SetRedeemAsync(context, inlineRedeem, ct);
                if (!redeemResp.Status)
                    return new SetVerifyResponse { Status = false };

                // Re-fetch the newly created pending request
                allPending = await _uow.Repository<RedeemRequest>().GetAllAsync(
                    rr => rr.LoyaltyCardId == cardForProgram.Id && rr.Status == RedeemStatus.Pending && rr.ExpiresUtc > DateTime.UtcNow);
                pendingRequest = allPending.OrderByDescending(rr => rr.Id).FirstOrDefault();
                if (pendingRequest == null)
                    return new SetVerifyResponse { Status = false };
            }

            // OrderId-ni set et ki, collect delete zamanı bu transaction da tapılsın
            if (!string.IsNullOrEmpty(request.OrderId) && string.IsNullOrEmpty(pendingRequest.OrderId))
            {
                pendingRequest.OrderId = request.OrderId;
                _uow.Repository<RedeemRequest>().Update(pendingRequest);
                await _uow.CommitAsync(ct);
            }

            var result = await _redeemService.ConfirmRedeemAsync(pendingRequest.Id, request.Otp, ct);
            if (!result.Success)
                return new SetVerifyResponse { Status = false };

            if (request.Type == "cashback")
            {
                var program = await _loyaltyProgramRepo.GetByIdAsync(programId.Value, ct);
                var pointsRedeemed = (result.OldPoints ?? 0) - (result.NewPoints ?? 0);
                if (pointsRedeemed < 0) pointsRedeemed = 0;
                var discount = request.Amount > 0 ? request.Amount : pointsRedeemed * (program?.CurrencyPerPoint ?? 0.1m);
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
                // Yalnız Earned və Redeemed transaction-ları al (Adjusted = artıq reverse olunub)
                var txns = (await _uow.Repository<Transaction>().GetAllAsync(
                    t => t.OrderId == request.Id && t.LoyaltyCardId == card.Id
                         && t.Type != TransactionType.Adjusted))
                    .OrderBy(t => t.Id).ToList();

                var c = await _uow.Repository<LoyaltyCard>().GetByIdAsync(card.Id, ct);
                if (c != null && txns.Count > 0)
                {
                    foreach (var tx in txns)
                    {
                        // BalanceAfter - əvvəlki tx-in BalanceAfter = real delta
                        // RedeemService Points=BalanceAfter saxlayır, webhook earn Points=delta saxlayır
                        // Ona görə BalanceAfter fərqindən istifadə edirik (TransactionReverseService kimi)
                        var prevTx = await _uow.Repository<Transaction>().GetAllAsync(
                            t => t.LoyaltyCardId == tx.LoyaltyCardId && t.Id < tx.Id);
                        var prev = prevTx.OrderByDescending(t => t.Id).FirstOrDefault();

                        decimal originalDelta = prev != null
                            ? tx.BalanceAfter - prev.BalanceAfter
                            : tx.BalanceAfter;

                        // Reverse: əks delta tətbiq et
                        c.CurrentSteps = Math.Max(0, c.CurrentSteps - (int)originalDelta);
                        c.CurrentPoints = Math.Max(0, c.CurrentPoints - originalDelta);
                        _uow.Repository<LoyaltyCard>().Update(c);

                        var reverseTx = new Transaction
                        {
                            LoyaltyCardId = card.Id,
                            Type = TransactionType.Adjusted,
                            Points = c.CurrentPoints,
                            OrderId = request.Id,
                            Description = $"POS order deleted (reverse {tx.Type}: {originalDelta:+#;-#;0})",
                            RegUserId = 1,
                            RegDate = DateTime.UtcNow,
                            IsActive = true,
                            BalanceAfter = c.CurrentPoints,
                            LocalOccurredAt = DateTimeOffset.UtcNow,
                            TimeZoneIdUsed = "UTC"
                        };
                        _uow.Repository<Transaction>().Add(reverseTx);
                    }
                    await _uow.CommitAsync(ct);
                }
                return new CollectResponse { Success = true };
            }

            var earnRules = await _earnRuleRepo.GetByTenantIntegrationIdAsync(tenantIntegrationId, ct);
            var rulesByProduct = earnRules.ToDictionary(r => r.ExternalProductId.Trim(), StringComparer.OrdinalIgnoreCase);

            int totalSteps = 0;
            decimal totalPoints = 0m;
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
                // HappyHoursWindows daxil olmaq üçün Include-lu çağırış
                var program = await _loyaltyProgramRepo.GetByIdNameAsync(programId.Value, ct);
                if (program != null)
                {
                    // Tier faizini hesabla (əgər CardTemplate varsa)
                    int? tierPercent = null;
                    int? tierPercentCash = null;
                    bool hasSplitPayment = false;

                    if (program.CardTemplateId.HasValue)
                    {
                        var tpl = await _cardTemplateRepo.GetByIdAsync(program.CardTemplateId.Value, ct);
                        if (tpl != null && tpl.PayMentType)
                        {
                            var (_, percent, percentCash, _) = ResolveTier(card.CurrentPoints, tpl);
                            tierPercent = percent;
                            tierPercentCash = percentCash ?? percent;
                            hasSplitPayment = true;
                        }
                        else if (tpl != null)
                        {
                            var (_, percent, _, _) = ResolveTier(card.CurrentPoints, tpl);
                            tierPercent = percent;
                        }
                    }

                    // HappyHours override: tenant local time ilə window/BasePercent
                    int? hhCash = null;
                    int? hhCard = null;
                    var hasWindows = program.HappyHoursWindows?.Count > 0;
                    var hasBase = program.BasePercentCash.HasValue || program.BasePercentCard.HasValue;
                    if (hasWindows || hasBase)
                    {
                        var (localTime, _) = await _timeZoneService.GetTenantLocalTimeAsync(context.TenantId, null, ct);
                        (hhCash, hhCard) = HappyHoursRateResolver.Resolve(program, localTime.DateTime);
                        _logger.LogInformation(
                            "Webhook HappyHours resolved. ProgramId={ProgramId} LocalTime={LocalTime} HhCash={HhCash} HhCard={HhCard}",
                            program.Id, localTime, hhCash, hhCard);
                    }

                    // HappyHours tier%-i override edir; null tərəflər tier-ə fallback
                    var effCash = hhCash ?? tierPercentCash ?? tierPercent;
                    var effCard = hhCard ?? tierPercent;
                    var hasHhSplit = hhCash.HasValue || hhCard.HasValue;

                    if ((hasSplitPayment || hasHhSplit) && request.Payments != null)
                    {
                        // PayMentType=true ya da HappyHours override varsa cash/cashless üçün ayrı faiz
                        var pctCard = effCard ?? 0;
                        var pctCash = effCash ?? pctCard;
                        var cashPoints = request.Payments.Cash * pctCash / 100m;
                        var cashlessPoints = request.Payments.Cashless * pctCard / 100m;
                        totalPoints = cashPoints + cashlessPoints;
                    }
                    else if (effCard.HasValue && effCard.Value > 0)
                    {
                        // Tek faiz (HappyHours card override və ya tier%) total-a tətbiq
                        totalPoints = request.Total * effCard.Value / 100m;
                    }
                    else if (program.PointsPerCurrency > 0)
                    {
                        // Faiz yoxdursa: total × PointsPerCurrency (kəsr saxlanır)
                        totalPoints = request.Total * program.PointsPerCurrency;
                    }
                }
            }

            if (totalSteps > 0 || totalPoints > 0)
            {
                var c = await _uow.Repository<LoyaltyCard>().GetByIdAsync(card.Id, ct);
                if (c != null)
                {
                    var prog = await _loyaltyProgramRepo.GetByIdAsync(programId.Value, ct);
                    var isProgressBased = false;
                    int maxSteps = 10;

                    if (prog?.CardTemplateId != null)
                    {
                        var tpl = await _cardTemplateRepo.GetByIdAsync(prog.CardTemplateId.Value, ct);
                        if (tpl != null && tpl.TemplateType == CardTemplateType.ProgressBased)
                        {
                            isProgressBased = true;
                            maxSteps = Math.Max(1, tpl.TotalSteps);
                        }
                    }

                    if (isProgressBased)
                    {
                        // ProgressBased: yalnız steps artır, CurrentPoints artırmır (steps integer)
                        // EarnRule varsa totalSteps, yoxdursa totalPoints-i step kimi istifadə et (kəsr atılır)
                        int stepsToAdd = totalSteps > 0 ? totalSteps : (int)totalPoints;
                        int newSteps = c.CurrentSteps + stepsToAdd;
                        int cyclesEarned = newSteps / maxSteps;
                        if (cyclesEarned > 0)
                            c.CompletedCycles += cyclesEarned;
                        c.CurrentSteps = newSteps % maxSteps;

                        // CurrentPoints-i də artır (ApplyDelta kimi) - cycle tracking üçün
                        c.CurrentPoints += stepsToAdd;
                        c.LifetimePoints += stepsToAdd;
                    }
                    else
                    {
                        // TierBased / PointsBased: yalnız points artır (kəsr saxlanır)
                        c.CurrentSteps += totalSteps;
                        c.CurrentPoints += totalPoints;
                        c.LifetimePoints += totalPoints;
                    }
                    _uow.Repository<LoyaltyCard>().Update(c);

                    decimal earnedValue = isProgressBased
                        ? (totalSteps > 0 ? totalSteps : (int)totalPoints)
                        : (totalSteps > 0 ? totalSteps : totalPoints);

                    var tx = new Transaction
                    {
                        LoyaltyCardId = card.Id,
                        Type = TransactionType.Earned,
                        Status = RedeemStatus.Applied,
                        Points = earnedValue,
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

        public async Task<IikoTransactionResponse> ProcessIikoTransactionAsync(
            WalletWebhookResolvedContext context, IikoTransactionRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.TransactionId))
                return new IikoTransactionResponse { Success = false, Message = "TransactionId is required" };

            if (string.IsNullOrWhiteSpace(request.CustomerId))
                return new IikoTransactionResponse { Success = false, Message = "CustomerId is required" };

            // 1) Idempotency: transactionId + walletId ilə artıq işlənib mi?
            var idempotencyKey = $"{request.TransactionId}:{request.WalletId}";
            var existing = await _uow.Repository<Transaction>().GetAllAsync(
                t => t.OrderId == idempotencyKey);
            if (existing.Any())
                return new IikoTransactionResponse { Success = true, Message = "Already processed (duplicate)" };

            // 2) PosCustomerMapping ilə müştərini tap
            var customerId = await _customerMappingRepo.GetCustomerIdByExternalIdAsync(
                context.TenantIntegrationId, request.CustomerId, ct);
            if (customerId == null)
                return new IikoTransactionResponse { Success = false, Message = $"Customer not found for externalId: {request.CustomerId}" };

            // 3) Aktiv LoyaltyCard tap
            if (context.LoyaltyProgramId == null)
                return new IikoTransactionResponse { Success = false, Message = "LoyaltyProgramId not configured" };

            var card = await _loyaltyCardRepo.GetLastActiveByCustomerAsync(
                context.TenantId, context.LoyaltyProgramId.Value, customerId.Value, ct);
            if (card == null)
                return new IikoTransactionResponse { Success = false, Message = "Active loyalty card not found" };

            // sum = delta (yalnız transaction log / LifetimePoints earn üçün); balance = absolute state.
            var delta = request.Sum;
            var txType = delta > 0 ? TransactionType.Earned : (delta < 0 ? TransactionType.Redeemed : TransactionType.Earned);

            // Program + CardTemplate yüklə (ProgressBased/PointsBased fərqi üçün)
            var program = await _loyaltyProgramRepo.GetByIdNameAsync(context.LoyaltyProgramId!.Value);

            var template = program?.CardTemplate;
            var templateType = template?.TemplateType ?? CardTemplateType.PointsBased;
            var totalSteps = template?.TotalSteps ?? 10;

            // iiko-nun authoritative `balance` field-i istifadə olunur (absolute state, drift yox).
            // `sum` (delta) yalnız Transaction log-da, LifetimePoints earn tracking və UI mesajı üçün saxlanır.
            var absBalance = Math.Max(0m, request.Balance);

            if (templateType == CardTemplateType.ProgressBased)
            {
                int absBalanceInt = (int)absBalance;
                card.CompletedCycles = absBalanceInt / totalSteps;
                card.CurrentSteps = absBalanceInt % totalSteps;
                card.CurrentPoints = absBalanceInt;
                if (delta > 0)
                    card.LifetimePoints += (int)delta;
            }
            else
            {
                // PointsBased/TierBased: iiko-nun absolute balance-i
                card.CurrentPoints = absBalance;
                if (delta > 0)
                    card.LifetimePoints += delta;
            }

            _loyaltyCardRepo.Update(card);

            // 5) Transaction yarat
            var tx = new Transaction
            {
                LoyaltyCardId = card.Id,
                Type = txType,
                Status = RedeemStatus.Applied,
                Points = delta,
                Amount = Math.Abs(request.Sum),
                BalanceAfter = card.CurrentPoints,
                OrderId = idempotencyKey,
                ReferenceNumber = request.TransactionId,
                Description = $"iiko {request.TransactionType}",
                BranchId = context.BranchId,
                RegDate = DateTime.UtcNow,
                IsActive = true,
                LocalOccurredAt = DateTimeOffset.UtcNow,
                TimeZoneIdUsed = "UTC"
            };

            _uow.Repository<Transaction>().Add(tx);
            await _uow.CommitAsync(ct);

            // Wallet pass push (Apple + Google) — DB commit-dən sonra fire-and-forget pattern
            try
            {
                await PushWalletUpdateAsync(card, program, delta, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko webhook: wallet push failed. CardId={CardId}", card.Id);
            }

            return new IikoTransactionResponse
            {
                Success = true,
                Message = templateType switch
                {
                    CardTemplateType.ProgressBased => $"Steps: {card.CurrentSteps}, FreeCoffees: {card.CompletedCycles}",
                    CardTemplateType.TierBased => $"Cashback earned: {delta}, Balance: {card.CurrentPoints}, Tier: {ResolveTier(card.LifetimePoints, template!).tierName}",
                    _ => $"Points: {card.CurrentPoints}"
                }
            };
        }

        public async Task<IikoTransactionResponse> ProcessIikoOrderEventAsync(
            WalletWebhookResolvedContext context, IikoOrderEventRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.OrderId))
                return new IikoTransactionResponse { Success = false, Message = "OrderId is required" };

            var eventType = request.EventType?.Trim();
            if (!string.Equals(eventType, "OrderClosed", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(eventType, "OrderReturned", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("iiko order event ignored. EventType={EventType} OrderId={OrderId}", eventType, request.OrderId);
                return new IikoTransactionResponse { Success = true, Message = $"EventType '{eventType}' ignored" };
            }

            if (context.LoyaltyProgramId == null)
                return new IikoTransactionResponse { Success = false, Message = "LoyaltyProgramId not configured" };

            int? customerId = null;
            if (!string.IsNullOrWhiteSpace(request.ExternalCustomerId))
            {
                customerId = await _customerMappingRepo.GetCustomerIdByExternalIdAsync(
                    context.TenantIntegrationId, request.ExternalCustomerId, ct);
            }
            if (customerId == null && !string.IsNullOrWhiteSpace(request.CustomerPhone))
            {
                var byPhone = await _customerRepo.GetByPhoneAsync(context.TenantId, request.CustomerPhone);
                customerId = byPhone?.Id;
            }
            // Fallback: iiko müştəri GUID-i / telefonu tapılmadıqda loyallıq kart nömrəsi ilə tap.
            // iiko payload-dakı Customer.CardNumber = bizim LoyaltyCard.CardNumber.
            if (customerId == null && !string.IsNullOrWhiteSpace(request.CustomerCardNumber))
            {
                var cardNo = request.CustomerCardNumber.Trim();
                var byCard = await _loyaltyCardRepo.GetByCardNumberAsync(context.TenantId, cardNo, ct);
                // iiko-da yalnız son 6 rəqəm saxlanılır → tam CardNumber tapılmasa suffix ilə axtar.
                if (byCard == null && cardNo.Length >= 4 && cardNo.Length <= 6)
                    byCard = await _loyaltyCardRepo.GetByCardNumberSuffixAsync(context.TenantId, cardNo, ct);
                customerId = byCard?.CustomerId;
            }
            if (customerId == null)
                return new IikoTransactionResponse { Success = false, Message = $"Customer not found (extId={request.ExternalCustomerId}, phone={request.CustomerPhone}, card={request.CustomerCardNumber})" };

            var card = await _loyaltyCardRepo.GetLastActiveByCustomerAsync(
                context.TenantId, context.LoyaltyProgramId.Value, customerId.Value, ct);
            if (card == null)
                return new IikoTransactionResponse { Success = false, Message = "Active loyalty card not found" };

            var program = await _loyaltyProgramRepo.GetByIdNameAsync(context.LoyaltyProgramId.Value);
            var template = program?.CardTemplate;
            var templateType = template?.TemplateType ?? CardTemplateType.PointsBased;
            var totalStepsPerCycle = template?.TotalSteps ?? 10;

            bool isReturn = string.Equals(eventType, "OrderReturned", StringComparison.OrdinalIgnoreCase);
            string idempotencyKey = $"iiko:{request.OrderId}:{(isReturn ? "OrderReturned" : "OrderClosed")}";

            var existing = await _uow.Repository<Transaction>().GetAllAsync(
                t => t.OrderId == idempotencyKey);
            if (existing.Any())
                return new IikoTransactionResponse { Success = true, Message = "Already processed (duplicate)" };

            if (isReturn)
            {
                return await ProcessIikoOrderReturnedAsync(card, request, idempotencyKey, templateType, totalStepsPerCycle, program, ct);
            }

            return await ProcessIikoOrderClosedAsync(context, card, request, idempotencyKey, templateType, totalStepsPerCycle, program, template, ct);
        }

        private async Task<IikoTransactionResponse> ProcessIikoOrderReturnedAsync(
            LoyaltyCard card, IikoOrderEventRequest request, string idempotencyKey,
            CardTemplateType templateType, int totalStepsPerCycle, LoyaltyProgram? program, CancellationToken ct)
        {
            string closedKeyPrefix = $"iiko:{request.OrderId}:OrderClosed";

            var prior = (await _uow.Repository<Transaction>().GetAllAsync(
                t => t.LoyaltyCardId == card.Id
                     && t.OrderId != null
                     && t.OrderId.StartsWith(closedKeyPrefix)
                     && t.Type != TransactionType.Adjusted))
                .OrderBy(t => t.Id).ToList();

            if (prior.Count == 0)
            {
                _logger.LogWarning("iiko OrderReturned: no prior OrderClosed tx found. OrderId={OrderId} CardId={CardId}",
                    request.OrderId, card.Id);
                return new IikoTransactionResponse { Success = true, Message = "No prior OrderClosed transaction to reverse" };
            }

            var c = await _uow.Repository<LoyaltyCard>().GetByIdAsync(card.Id, ct);
            if (c == null)
                return new IikoTransactionResponse { Success = false, Message = "Loyalty card vanished mid-flight" };

            decimal totalReverseDelta = 0m;

            foreach (var tx in prior)
            {
                // Original earn delta = tx.Points (what was actually awarded).
                // BalanceAfter delta approach fragile if intermediate tx-lər (marker, başqa earn) varsa.
                decimal originalDelta = tx.Points;

                totalReverseDelta += originalDelta;

                if (templateType == CardTemplateType.ProgressBased)
                {
                    int deltaInt = (int)originalDelta;
                    int newAbs = Math.Max(0, (int)c.CurrentPoints - deltaInt);
                    c.CurrentPoints = newAbs;
                    int maxSteps = Math.Max(1, totalStepsPerCycle);
                    c.CurrentSteps = newAbs % maxSteps;
                    c.CompletedCycles = newAbs / maxSteps;
                    // LifetimePoints da geri qaytar (cumulative ləkədən təmizlə)
                    c.LifetimePoints = Math.Max(0m, c.LifetimePoints - originalDelta);
                }
                else
                {
                    c.CurrentPoints = Math.Max(0m, c.CurrentPoints - originalDelta);
                    c.LifetimePoints = Math.Max(0m, c.LifetimePoints - originalDelta);
                }

                _uow.Repository<LoyaltyCard>().Update(c);

                var reverseTx = new Transaction
                {
                    LoyaltyCardId = card.Id,
                    Type = TransactionType.Adjusted,
                    Status = RedeemStatus.Applied,
                    Points = -originalDelta,
                    Amount = Math.Abs(originalDelta),
                    OrderId = idempotencyKey,
                    Description = $"iiko OrderReturned reverse {tx.Type} ({originalDelta:+#;-#;0})",
                    BranchId = tx.BranchId,
                    RegUserId = 1,
                    RegDate = DateTime.UtcNow,
                    IsActive = true,
                    BalanceAfter = c.CurrentPoints,
                    LocalOccurredAt = DateTimeOffset.UtcNow,
                    TimeZoneIdUsed = "UTC"
                };
                _uow.Repository<Transaction>().Add(reverseTx);
            }

            await _uow.CommitAsync(ct);

            try { await PushWalletUpdateAsync(c, program, -totalReverseDelta, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "iiko OrderReturned: wallet push failed. CardId={CardId}", c.Id); }

            return new IikoTransactionResponse
            {
                Success = true,
                Message = $"Reversed {prior.Count} tx, totalDelta={totalReverseDelta}, balance={c.CurrentPoints}"
            };
        }

        private async Task<IikoTransactionResponse> ProcessIikoOrderClosedAsync(
            WalletWebhookResolvedContext context, LoyaltyCard card, IikoOrderEventRequest request, string idempotencyKey,
            CardTemplateType templateType, int totalStepsPerCycle, LoyaltyProgram? program, CardTemplate? template, CancellationToken ct)
        {
            decimal walletRedeemSum = request.Payments
                .Where(p => !string.IsNullOrWhiteSpace(p.Credential))
                .Sum(p => p.Sum);

            // ProgressBased-də iikoCard pay ignore olunur (məntiqsiz manat→step conversion).
            // Earn isə item count əsasındadır, ödəniş növündən asılı olmasın.
            decimal earnBase = templateType == CardTemplateType.ProgressBased
                ? request.TotalSum
                : Math.Max(0m, request.TotalSum - walletRedeemSum);

            // === Loyalty discount redemption (Discounts.DiscountByItem) ===
            var loyaltyDiscountIds = (await _loyaltyDiscountTypeRepo.GetByTenantIntegrationIdAsync(context.TenantIntegrationId, ct))
                .Select(d => d.ExternalDiscountTypeId.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // DiscountByItem yalnız ProductId (GUID) göndərir; reward-lar ProductNumber ilə açarlanıb.
            // Items[]-dən GUID→ProductNumber map qur, redeem açarını ProductNumber-ə (varsa) çevir.
            var numberByProductId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in request.Items)
            {
                var gid = (it.ProductId ?? "").Trim();
                var num = (it.ProductNumber ?? "").Trim();
                if (gid.Length > 0 && num.Length > 0)
                    numberByProductId[gid] = num;
            }

            // Aktiv reward-lar (ProductNumber → reward). Həm precise (DiscountByItem),
            // həm də fallback (DiscountByItem-siz) yol üçün əvvəldən yüklə.
            var rewardByProductId = new Dictionary<string, Reward>(StringComparer.OrdinalIgnoreCase);
            if (context.LoyaltyProgramId.HasValue)
            {
                var activeRewards = await _rewardRepo.GetActiveRewardsByProgramIdAsync(context.LoyaltyProgramId.Value, ct);
                rewardByProductId = activeRewards
                    .Where(r => !string.IsNullOrWhiteSpace(r.ExternalProductId))
                    .GroupBy(r => r.ExternalProductId!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            }

            var redeemedUnitsByProduct = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool hasLoyaltyDiscount = false;
            foreach (var d in request.Discounts ?? new List<IikoOrderDiscount>())
            {
                var dtid = (d.DiscountTypeId ?? "").Trim();
                if (string.IsNullOrEmpty(dtid) || !loyaltyDiscountIds.Contains(dtid)) continue;
                hasLoyaltyDiscount = true;
                foreach (var dbi in d.DiscountByItem ?? new List<IikoOrderDiscountByItem>())
                {
                    var pid = (dbi.ProductId ?? "").Trim();
                    if (string.IsNullOrEmpty(pid)) continue;
                    // Reward match üçün ProductNumber-ə çevir (tapılmasa GUID qalsın).
                    var key = numberByProductId.TryGetValue(pid, out var num) ? num : pid;
                    redeemedUnitsByProduct[key] = redeemedUnitsByProduct.GetValueOrDefault(key) + 1;
                }
            }

            // Fallback: bəzi iiko konfiqləri loyalty endirimini DiscountByItem-siz göndərir
            // (yalnız order-level DiscountSum, məs. "Донер в подарок"). Bu halda sifarişdəki
            // reward-məhsulları pulsuz verilmiş kimi say.
            if (hasLoyaltyDiscount && redeemedUnitsByProduct.Count == 0 && rewardByProductId.Count > 0)
            {
                foreach (var item in request.Items)
                {
                    var pnum = (item.ProductNumber ?? "").Trim();
                    var pid = (item.ProductId ?? "").Trim();
                    string? key = pnum.Length > 0 && rewardByProductId.ContainsKey(pnum) ? pnum
                        : (pid.Length > 0 && rewardByProductId.ContainsKey(pid) ? pid : null);
                    if (key == null) continue;
                    int units = Math.Max(1, (int)item.Amount);
                    redeemedUnitsByProduct[key] = redeemedUnitsByProduct.GetValueOrDefault(key) + units;
                }
            }

            int totalRedeemUnits = 0;
            decimal totalRedeemPointsCost = 0m;
            if (redeemedUnitsByProduct.Count > 0)
            {
                foreach (var (pid, count) in redeemedUnitsByProduct)
                {
                    if (rewardByProductId.TryGetValue(pid, out var reward))
                    {
                        totalRedeemUnits += count;
                        totalRedeemPointsCost += reward.PointsCost * count;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "iiko OrderClosed: loyalty discount for productId={Pid} but no active reward mapped. OrderId={OrderId} TenantId={TenantId}",
                            pid, request.OrderId, context.TenantId);
                    }
                }
            }

            decimal totalEarnPoints = 0m;
            int totalEarnSteps = 0;
            bool anyEarnRuleMatched = false;

            if (earnBase > 0)
            {
                var earnRules = await _earnRuleRepo.GetByTenantIntegrationIdAsync(context.TenantIntegrationId, ct);
                var rulesByProduct = earnRules.ToDictionary(r => r.ExternalProductId.Trim(), StringComparer.OrdinalIgnoreCase);

                foreach (var item in request.Items)
                {
                    var pid = (item.ProductId ?? "").Trim();
                    var pnum = (item.ProductNumber ?? "").Trim();
                    if (string.IsNullOrEmpty(pid) && string.IsNullOrEmpty(pnum)) continue;
                    // Redeem açarı ProductNumber (varsa) və ya GUID ola bilər — hər ikisini yoxla.
                    if ((pid.Length > 0 && redeemedUnitsByProduct.ContainsKey(pid)) ||
                        (pnum.Length > 0 && redeemedUnitsByProduct.ContainsKey(pnum)))
                    {
                        // bu item loyalty discount-da iştirak edib → earn-dən düşür
                        continue;
                    }
                    // Earn rule ProductId (GUID) və ya ProductNumber (iiko nomenklatura kodu) ilə uyğunlaşa bilər.
                    // iiko loyalty proqramları məhsulları kod (ProductNumber) ilə saxlayır.
                    PosProductEarnRule? rule = null;
                    if (pid.Length > 0)
                        rulesByProduct.TryGetValue(pid, out rule);
                    if (rule == null && pnum.Length > 0)
                        rulesByProduct.TryGetValue(pnum, out rule);
                    if (rule != null)
                    {
                        anyEarnRuleMatched = true;
                        totalEarnSteps += rule.StepsPerUnit * (int)item.Amount;
                        totalEarnPoints += rule.PointsPerUnit * item.Amount;
                    }
                }

                if (!anyEarnRuleMatched && program != null)
                {
                    int? tierPercent = null;
                    if (template != null)
                    {
                        var (_, percent, _, _) = ResolveTier(card.CurrentPoints, template);
                        tierPercent = percent;
                    }

                    int? hhCash = null;
                    int? hhCard = null;
                    var hasWindows = program.HappyHoursWindows?.Count > 0;
                    var hasBase = program.BasePercentCash.HasValue || program.BasePercentCard.HasValue;
                    if (hasWindows || hasBase)
                    {
                        var (localTime, _) = await _timeZoneService.GetTenantLocalTimeAsync(context.TenantId, null, ct);
                        (hhCash, hhCard) = HappyHoursRateResolver.Resolve(program, localTime.DateTime);
                    }

                    var effPct = hhCard ?? tierPercent;

                    if (effPct.HasValue && effPct.Value > 0)
                        totalEarnPoints = earnBase * effPct.Value / 100m;
                    else if (program.PointsPerCurrency > 0)
                        totalEarnPoints = earnBase * program.PointsPerCurrency;
                }
            }

            var c = await _uow.Repository<LoyaltyCard>().GetByIdAsync(card.Id, ct);
            if (c == null)
                return new IikoTransactionResponse { Success = false, Message = "Loyalty card vanished mid-flight" };

            decimal earnedValue = 0m;
            if (totalEarnPoints > 0 || totalEarnSteps > 0)
            {
                if (templateType == CardTemplateType.ProgressBased)
                {
                    int stepsToAdd = totalEarnSteps > 0 ? totalEarnSteps : (int)totalEarnPoints;
                    int maxSteps = Math.Max(1, totalStepsPerCycle);
                    int newSteps = c.CurrentSteps + stepsToAdd;
                    c.CompletedCycles += newSteps / maxSteps;
                    c.CurrentSteps = newSteps % maxSteps;
                    c.CurrentPoints += stepsToAdd;
                    c.LifetimePoints += stepsToAdd;
                    earnedValue = stepsToAdd;
                }
                else
                {
                    c.CurrentSteps += totalEarnSteps;
                    c.CurrentPoints += totalEarnPoints;
                    c.LifetimePoints += totalEarnPoints;
                    earnedValue = totalEarnSteps > 0 ? totalEarnSteps : totalEarnPoints;
                }

                _uow.Repository<LoyaltyCard>().Update(c);

                var earnTx = new Transaction
                {
                    LoyaltyCardId = c.Id,
                    Type = TransactionType.Earned,
                    Status = RedeemStatus.Applied,
                    Points = earnedValue,
                    Amount = earnBase,
                    OrderId = idempotencyKey + ":earn",
                    ReferenceNumber = request.OrderNumber?.ToString(),
                    Description = "iiko OrderClosed earn",
                    BranchId = context.BranchId,
                    RegUserId = 1,
                    RegDate = DateTime.UtcNow,
                    IsActive = true,
                    BalanceAfter = c.CurrentPoints,
                    LocalOccurredAt = DateTimeOffset.UtcNow,
                    TimeZoneIdUsed = "UTC"
                };
                _uow.Repository<Transaction>().Add(earnTx);
            }

            // ProgressBased-də manat→step blind conversion mənasız. Reward konsum yalnız Discounts.DiscountByItem yolu ilə.
            if (walletRedeemSum > 0 && templateType != CardTemplateType.ProgressBased)
            {
                c.CurrentPoints = Math.Max(0m, c.CurrentPoints - walletRedeemSum);

                _uow.Repository<LoyaltyCard>().Update(c);

                var redeemTx = new Transaction
                {
                    LoyaltyCardId = c.Id,
                    Type = TransactionType.Redeemed,
                    Status = RedeemStatus.Applied,
                    Points = -walletRedeemSum,
                    Amount = walletRedeemSum,
                    OrderId = idempotencyKey + ":redeem",
                    ReferenceNumber = request.OrderNumber?.ToString(),
                    Description = "iiko OrderClosed wallet pay",
                    BranchId = context.BranchId,
                    RegUserId = 1,
                    RegDate = DateTime.UtcNow,
                    IsActive = true,
                    BalanceAfter = c.CurrentPoints,
                    LocalOccurredAt = DateTimeOffset.UtcNow,
                    TimeZoneIdUsed = "UTC"
                };
                _uow.Repository<Transaction>().Add(redeemTx);
            }
            else if (walletRedeemSum > 0 && templateType == CardTemplateType.ProgressBased)
            {
                _logger.LogInformation(
                    "iiko OrderClosed: walletRedeemSum={Sum} ignored for ProgressBased card. OrderId={OrderId} CardId={CardId}",
                    walletRedeemSum, request.OrderId, c.Id);
            }

            // === Discount-based redeem (DiscountByItem → reward consumed) ===
            decimal discountRedeemPoints = 0m;
            if (totalRedeemUnits > 0)
            {
                if (templateType == CardTemplateType.ProgressBased)
                {
                    int cyclesToConsume = Math.Min(c.CompletedCycles, totalRedeemUnits);
                    c.CompletedCycles -= cyclesToConsume;
                    int pointsDec = cyclesToConsume * Math.Max(1, totalStepsPerCycle);
                    c.CurrentPoints = Math.Max(0m, c.CurrentPoints - pointsDec);
                    discountRedeemPoints = pointsDec;
                }
                else
                {
                    decimal cost = Math.Min(c.CurrentPoints, totalRedeemPointsCost);
                    c.CurrentPoints = Math.Max(0m, c.CurrentPoints - cost);
                    discountRedeemPoints = cost;
                }

                _uow.Repository<LoyaltyCard>().Update(c);

                var discountRedeemTx = new Transaction
                {
                    LoyaltyCardId = c.Id,
                    Type = TransactionType.Redeemed,
                    Status = RedeemStatus.Applied,
                    Points = -discountRedeemPoints,
                    Amount = 0m,
                    OrderId = idempotencyKey + ":discount_redeem",
                    ReferenceNumber = request.OrderNumber?.ToString(),
                    Description = $"iiko discount loyalty redeem ({totalRedeemUnits} units)",
                    BranchId = context.BranchId,
                    RegUserId = 1,
                    RegDate = DateTime.UtcNow,
                    IsActive = true,
                    BalanceAfter = c.CurrentPoints,
                    LocalOccurredAt = DateTimeOffset.UtcNow,
                    TimeZoneIdUsed = "UTC"
                };
                _uow.Repository<Transaction>().Add(discountRedeemTx);
            }

            bool walletRedeemApplied = walletRedeemSum > 0 && templateType != CardTemplateType.ProgressBased;
            if (earnedValue > 0 || walletRedeemApplied || totalRedeemUnits > 0)
            {
                // marker tx — duplicate guard üçün (idempotency key tam match)
                var markerTx = new Transaction
                {
                    LoyaltyCardId = c.Id,
                    Type = TransactionType.Adjusted,
                    Status = RedeemStatus.Applied,
                    Points = c.CurrentPoints,
                    Amount = request.TotalSum,
                    OrderId = idempotencyKey,
                    ReferenceNumber = request.OrderNumber?.ToString(),
                    Description = $"iiko OrderClosed marker (earn={earnedValue}, redeem={walletRedeemSum}, discountRedeemUnits={totalRedeemUnits})",
                    BranchId = context.BranchId,
                    RegUserId = 1,
                    RegDate = DateTime.UtcNow,
                    IsActive = true,
                    BalanceAfter = c.CurrentPoints,
                    LocalOccurredAt = DateTimeOffset.UtcNow,
                    TimeZoneIdUsed = "UTC"
                };
                _uow.Repository<Transaction>().Add(markerTx);
            }
            else
            {
                // earn da, redeem də yoxdursa marker hələ də idempotency üçün lazımdır
                var markerTx = new Transaction
                {
                    LoyaltyCardId = c.Id,
                    Type = TransactionType.Adjusted,
                    Status = RedeemStatus.Applied,
                    Points = c.CurrentPoints,
                    Amount = request.TotalSum,
                    OrderId = idempotencyKey,
                    ReferenceNumber = request.OrderNumber?.ToString(),
                    Description = "iiko OrderClosed marker (no-op)",
                    BranchId = context.BranchId,
                    RegUserId = 1,
                    RegDate = DateTime.UtcNow,
                    IsActive = true,
                    BalanceAfter = c.CurrentPoints,
                    LocalOccurredAt = DateTimeOffset.UtcNow,
                    TimeZoneIdUsed = "UTC"
                };
                _uow.Repository<Transaction>().Add(markerTx);
            }

            await _uow.CommitAsync(ct);

            decimal effectiveWalletRedeem = walletRedeemApplied ? walletRedeemSum : 0m;
            decimal netDelta = earnedValue - effectiveWalletRedeem - discountRedeemPoints;
            try { await PushWalletUpdateAsync(c, program, netDelta, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "iiko OrderClosed: wallet push failed. CardId={CardId}", c.Id); }

            string msg = templateType switch
            {
                CardTemplateType.ProgressBased => $"Steps: {c.CurrentSteps}, FreeCoffees: {c.CompletedCycles}",
                CardTemplateType.TierBased => $"Earned: {earnedValue}, Redeemed: {walletRedeemSum}, Balance: {c.CurrentPoints}, Tier: {(template != null ? ResolveTier(c.LifetimePoints, template).tierName : "-")}",
                _ => $"Earned: {earnedValue}, Redeemed: {walletRedeemSum}, Balance: {c.CurrentPoints}"
            };

            return new IikoTransactionResponse { Success = true, Message = msg };
        }

        private async Task PushWalletUpdateAsync(LoyaltyCard card, LoyaltyProgram? program, decimal delta, CancellationToken ct)
        {
            if (program?.CardTemplate == null)
            {
                _logger.LogDebug("Wallet push skipped: program/CardTemplate is null. CardId={CardId}", card.Id);
                return;
            }

            var branding = await _brandingRepo.GetByTenantIdAsync(card.TenantId, ct);
            const string baseUrl = "https://appapi.walvero.com";
            string ToAbs(string? dbPath)
            {
                if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                if (dbPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    dbPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return dbPath;
                var normalizedBaseUrl = baseUrl.TrimEnd('/', '\\');
                var normalizedPath = dbPath.TrimStart('~').TrimStart('/', '\\');
                normalizedPath = string.IsNullOrWhiteSpace(normalizedPath) ? "/" : "/" + normalizedPath;
                return normalizedBaseUrl + normalizedPath;
            }

            var snapshot = CardTemplateSnapshotFactory.From(program, program.CardTemplate, branding, ToAbs);
            var primaryPlatform = card.IsIOS ? WalletPlatform.AppleWallet : WalletPlatform.GooglePay;
            var secondaryPlatform = card.IsIOS ? WalletPlatform.GooglePay : WalletPlatform.AppleWallet;
            var intDelta = (int)delta;

            // ⚠️ CancellationToken.None: DB commit tamamlandıqdan sonra push ləğv olunmamalıdır
            _logger.LogInformation("iiko webhook: routing wallet push to PRIMARY {Platform}. CardId={CardId}", primaryPlatform, card.Id);
            try
            {
                var primaryRes = await _walletRouter.RouteAsync(
                    primaryPlatform, card.Id, snapshot,
                    card.CurrentPoints, card.CurrentSteps, card.CompletedCycles, intDelta,
                    CancellationToken.None);
                if (!primaryRes.Success)
                    _logger.LogWarning("iiko webhook: wallet push FAILED on PRIMARY {Platform}. CardId={CardId}", primaryPlatform, card.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko webhook: wallet push error on PRIMARY {Platform}. CardId={CardId}", primaryPlatform, card.Id);
            }

            _logger.LogInformation("iiko webhook: trying SECONDARY {Platform}. CardId={CardId}", secondaryPlatform, card.Id);
            try
            {
                var secondaryRes = await _walletRouter.RouteAsync(
                    secondaryPlatform, card.Id, snapshot,
                    card.CurrentPoints, card.CurrentSteps, card.CompletedCycles, intDelta,
                    CancellationToken.None);
                if (!secondaryRes.Success)
                    _logger.LogWarning("iiko webhook: wallet push FAILED on SECONDARY {Platform}. CardId={CardId}", secondaryPlatform, card.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko webhook: wallet push error on SECONDARY {Platform}. CardId={CardId}", secondaryPlatform, card.Id);
            }
        }

    }
}
