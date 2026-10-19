using Hangfire;
using Hangfire.Storage;
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
        private readonly IWelcomeBonusService _welcomeBonus;
        private readonly IEarnExclusionResolver _earnExclusions;

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
            IUnitOfWork uow,
            IWelcomeBonusService welcomeBonus,
            IEarnExclusionResolver earnExclusions)
        {
            _welcomeBonus = welcomeBonus;
            _earnExclusions = earnExclusions;
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
        /// Bonus istisna xəritəsi. Xəta olsa (məs. cədvəl hələ yoxdur) sifariş istisnasız emal olunur —
        /// istisna ikinci dərəcəlidir, sifariş emalını dayandırmamalıdır.
        /// </summary>
        private async Task<EarnExclusionMap> LoadExclusionMapAsync(int tenantIntegrationId, CancellationToken ct)
        {
            try
            {
                return await _earnExclusions.GetMapAsync(tenantIntegrationId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Earn exclusion map unavailable, order processed without exclusions. TenantIntegrationId={Id}",
                    tenantIntegrationId);
                return EarnExclusionMap.Empty;
            }
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

        /// <summary>
        /// Barkod/WalveroId ilə müştərinin AKTİV kartı (proqramından asılı olmayaraq — bir müştəri = bir aktiv kart,
        /// məs. ambassador kartı inteqrasiyanın proqramından fərqli ola bilər). Bağlı kart tapılarsa aktiv karta keçir.
        /// </summary>
        private async Task<LoyaltyCard?> ResolveActiveCardAsync(int tenantId, string rawBarcode, CancellationToken ct)
        {
            var card = await ResolveCardAsync(tenantId, rawBarcode, null, ct);
            if (card == null || card.IsActive) return card;
            return await _loyaltyCardRepo.GetActiveByCustomerAsync(tenantId, card.CustomerId, ct) ?? card;
        }

        public async Task<GetWalletResponse> GetWalletAsync(WalletWebhookResolvedContext context, string barcode, CancellationToken ct = default)
        {
            var tenantId = context.TenantId;

            var card = await ResolveActiveCardAsync(tenantId, barcode, ct);
            if (card == null)
                return new GetWalletResponse { Success = false, Message = "Customer not found" };

            // Kartın öz proqramı (ambassador kartı inteqrasiyanın proqramından fərqli ola bilər)
            int? programId = card.LoyaltyProgramId;
            var program = await _loyaltyProgramRepo.GetByIdAsync(programId.Value, ct);
            if (program == null)
                return new GetWalletResponse { Success = false, Message = "Program not found" };

            var cardInProgram = card;

            var loyalty = new List<LoyaltyCampaignItem>();
            double cashbackBalans = 0;

            if (cardInProgram != null)
            {
                // ProgressBased üçün cashback_balans göstərilmir; son istifadə tarixi keçmiş balans POS-a 0 kimi gedir
                if (program.ProgramType != LoyaltyProgramType.ProgressBased && !Helpers.PointsExpiry.IsExpired(cardInProgram.ExpiresAt))
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

            var card = await ResolveActiveCardAsync(tenantId, request.Barcode, ct);
            if (card == null)
                return new SetRedeemResponse { Status = false, Message = "Customer not found" };

            // Kartın öz proqramı
            programId = card.LoyaltyProgramId;
            var cardForProgram = card;

            decimal delta = request.Delta;

            var program = await _loyaltyProgramRepo.GetByIdAsync(programId.Value, ct);
            if (program != null && program.CardTemplateId.HasValue)
            {
                var tpl = await _cardTemplateRepo.GetByIdAsync(program.CardTemplateId.Value, ct);
                if (tpl != null && tpl.EffectiveType == CardTemplateType.ProgressBased)
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

            var card = await ResolveActiveCardAsync(tenantId, request.Barcode, ct);
            if (card == null)
                return new SetVerifyResponse { Status = false };

            // Kartın öz proqramı
            programId = card.LoyaltyProgramId;
            var cardForProgram = card;

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

            // Müştərinin aktiv kartı (proqramından asılı olmayaraq) — qaydalar/faiz kartın proqramından
            var card = await _loyaltyCardRepo.GetActiveByCustomerAsync(context.TenantId, customerId.Value, ct);
            if (card == null)
                return new CollectResponse { Success = false };
            programId = card.LoyaltyProgramId;

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
            var rulesByProduct = PosEarnRouting.RulesForProgram(earnRules, card.LoyaltyProgramId);
            // Super admin istisnaları: Monyo item-lərində qiymət yoxdur → yalnız möhür/məhsul xalı bloku tətbiq olunur.
            var exclusionMap = await LoadExclusionMapAsync(tenantIntegrationId, ct);

            int totalSteps = 0;
            decimal totalPoints = 0m;
            bool blockedRuleMatched = false;
            foreach (var item in request.Items)
            {
                var productIdStr = item.Id.ToString().Trim();
                if (rulesByProduct.TryGetValue(productIdStr, out var rule))
                {
                    if (exclusionMap.Blocked(productIdStr, null, null)?.BlockStamps == true)
                    {
                        // Qayda var, amma bloklanıb — cashback fallback-ı da açılmasın (rejim dəyişməsin).
                        blockedRuleMatched = true;
                        continue;
                    }
                    totalSteps += rule.StepsPerUnit * item.Count;
                    totalPoints += rule.PointsPerUnit * item.Count;
                }
            }

            if (totalSteps == 0 && totalPoints == 0 && !blockedRuleMatched)
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
                    var rewardAsPoints = false;
                    int maxSteps = 10;

                    if (prog?.CardTemplateId != null)
                    {
                        var tpl = await _cardTemplateRepo.GetByIdAsync(prog.CardTemplateId.Value, ct);
                        if (tpl != null && tpl.EffectiveType == CardTemplateType.ProgressBased)
                        {
                            isProgressBased = true;
                            maxSteps = Math.Max(1, tpl.TotalSteps);
                        }
                        rewardAsPoints = tpl?.RewardAsPoints == true;
                    }

                    // Xal rejimi: earn rule-lar StepsPerUnit ilə qurulub (1 məhsul = 1 addım) → addımları xal say.
                    if (rewardAsPoints && totalPoints == 0m && totalSteps > 0)
                    {
                        totalPoints = totalSteps;
                        totalSteps = 0;
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

                    decimal welcomeBonus = 0m;
                    if (prog != null)
                    {
                        welcomeBonus = await _welcomeBonus.TryGrantFirstPurchaseAsync(c, prog,
                            isProgressBased ? CardTemplateType.ProgressBased : CardTemplateType.PointsBased, maxSteps,
                            request.Total, totalPoints, context.BranchId, request.Id, ct);
                        if (welcomeBonus > 0) c.LifetimePoints += welcomeBonus;
                    }
                    await _uow.CommitAsync(ct);

                    EnqueueWalletPush(c.Id, prog, earnedValue + welcomeBonus);
                    if (welcomeBonus > 0)
                        await _welcomeBonus.SyncPosBalanceSafeAsync(context.TenantId, c.CustomerId, c.CurrentPoints, ct);
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

            // 3) Müştərinin aktiv kartı (proqramından asılı olmayaraq — ambassador kartı ola bilər)
            var card = await _loyaltyCardRepo.GetActiveByCustomerAsync(context.TenantId, customerId.Value, ct);
            if (card == null)
                return new IikoTransactionResponse { Success = false, Message = "Active loyalty card not found" };

            // sum = delta (yalnız transaction log / LifetimePoints earn üçün); balance = absolute state.
            var delta = request.Sum;
            var txType = delta > 0 ? TransactionType.Earned : (delta < 0 ? TransactionType.Redeemed : TransactionType.Earned);

            // Kartın proqramı + CardTemplate (ProgressBased/PointsBased fərqi üçün)
            var program = await _loyaltyProgramRepo.GetByIdNameAsync(card.LoyaltyProgramId);

            var template = program?.CardTemplate;
            var templateType = template?.EffectiveType ?? CardTemplateType.PointsBased;
            var totalSteps = template?.TotalSteps ?? 10;

            // Kartın proqramına başqa iiko cüzdanı bağlıdır (məs. ambassador-un 5% keşbeki Cashback cüzdanına gəlir) —
            // bu tranzaksiya kartın balansını (məhsul sayı) əvəz etməməlidir → yalnız marker
            var otherWallet = program != null && !IikoWalletSelection.MirrorAppliesToCard(program.PosWalletId, request.WalletId);

            // WalveroRules proqramı: qazancı Walvero hesablayır — iiko cüzdan güzgüsü balansa toxunmur (yalnız marker)
            if (program != null && (otherWallet || !PosEarnRouting.MirrorWritesBalance(program.PosEarnSource)))
            {
                _uow.Repository<Transaction>().Add(new Transaction
                {
                    LoyaltyCardId = card.Id,
                    Type = TransactionType.Adjusted,
                    Status = RedeemStatus.Applied,
                    Points = 0,
                    BalanceAfter = card.CurrentPoints,
                    OrderId = idempotencyKey,
                    ReferenceNumber = request.TransactionId,
                    Description = otherWallet
                        ? $"iiko {request.TransactionType} {request.Sum:0.##} (başqa iiko cüzdanı — balans dəyişmədi)"
                        : $"iiko {request.TransactionType} {request.Sum:0.##} (proqram Walvero qaydaları ilə — balans dəyişmədi)",
                    BranchId = context.BranchId,
                    RegDate = DateTime.UtcNow,
                    IsActive = true,
                    LocalOccurredAt = DateTimeOffset.UtcNow,
                    TimeZoneIdUsed = "UTC"
                });
                await _uow.CommitAsync(ct);
                return new IikoTransactionResponse
                {
                    Success = true,
                    Message = otherWallet ? "Wallet not linked to card's program — not mirrored" : "Program uses Walvero rules — iiko balance not mirrored"
                };
            }

            // iiko-nun authoritative `balance` field-i istifadə olunur (absolute state, drift yox).
            // `sum` (delta) yalnız Transaction log-da, LifetimePoints earn tracking və UI mesajı üçün saxlanır.
            var absBalance = Math.Max(0m, request.Balance);

            // Echo guard: bizim POS balans sync-imiz (welcome bonus / ambassador topup) iiko-dan eyni balansla
            // geri gəlir — dublikat Earned yaratma, yalnız idempotency marker saxla.
            if (delta > 0 && Math.Abs(absBalance - card.CurrentPoints) < 0.005m)
            {
                _uow.Repository<Transaction>().Add(new Transaction
                {
                    LoyaltyCardId = card.Id,
                    Type = TransactionType.Adjusted,
                    Status = RedeemStatus.Applied,
                    Points = 0,
                    BalanceAfter = card.CurrentPoints,
                    OrderId = idempotencyKey,
                    ReferenceNumber = request.TransactionId,
                    Description = "iiko balance echo (already applied locally)",
                    BranchId = context.BranchId,
                    RegDate = DateTime.UtcNow,
                    IsActive = true,
                    LocalOccurredAt = DateTimeOffset.UtcNow,
                    TimeZoneIdUsed = "UTC"
                });
                await _uow.CommitAsync(ct);
                return new IikoTransactionResponse { Success = true, Message = "Echo of local balance sync (skipped)" };
            }

            // Son istifadə tarixi keçmiş kart: iiko-nun balansı qəbul edilmir (bitmiş balans geri dirilməsin) —
            // yalnız marker yazılır və iiko wallet yenidən 0-a çəkilir. Job iiko 0 təsdiqindən sonra ExpiresAt-i silir.
            if (Helpers.PointsExpiry.IsExpired(card.ExpiresAt) && absBalance > 0)
            {
                _logger.LogWarning("iiko transaction on expired card ignored. CardId={CardId}, ExpiresAt={ExpiresAt}, iikoBalance={Balance}",
                    card.Id, card.ExpiresAt, absBalance);
                _uow.Repository<Transaction>().Add(new Transaction
                {
                    LoyaltyCardId = card.Id,
                    Type = TransactionType.Adjusted,
                    Status = RedeemStatus.Applied,
                    Points = 0,
                    BalanceAfter = card.CurrentPoints,
                    OrderId = idempotencyKey,
                    ReferenceNumber = request.TransactionId,
                    Description = "iiko balance ignored (points expired)",
                    BranchId = context.BranchId,
                    RegDate = DateTime.UtcNow,
                    IsActive = true,
                    LocalOccurredAt = DateTimeOffset.UtcNow,
                    TimeZoneIdUsed = "UTC"
                });
                await _uow.CommitAsync(ct);
                await _welcomeBonus.SyncPosBalanceSafeAsync(context.TenantId, card.CustomerId, 0m, ct);
                return new IikoTransactionResponse { Success = true, Message = "Points expired — iiko balance re-synced to 0" };
            }

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

            // Payload-da sifariş məbləği yoxdur → yalnız Fixed tipli bonus işləyir
            decimal welcomeBonus = 0m;
            if (delta > 0 && program != null)
            {
                welcomeBonus = await _welcomeBonus.TryGrantFirstPurchaseAsync(card, program, templateType, totalSteps,
                    null, delta, context.BranchId, idempotencyKey + ":welcome", ct);
                if (welcomeBonus > 0) card.LifetimePoints += welcomeBonus;
            }
            await _uow.CommitAsync(ct);

            // Wallet pass push — Hangfire background (webhook cavabı gözlətməsin → 499 qarşısı)
            EnqueueWalletPush(card.Id, program, delta + welcomeBonus);
            if (welcomeBonus > 0)
                await _welcomeBonus.SyncPosBalanceSafeAsync(context.TenantId, card.CustomerId, card.CurrentPoints, ct);

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

        private static IikoTransactionResponse IikoFail(IikoFailureKind kind, string message) =>
            new() { Success = false, Message = message, FailureKind = kind };

        public async Task<IikoTransactionResponse> ProcessIikoOrderEventAsync(
            WalletWebhookResolvedContext context, IikoOrderEventRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.OrderId))
                return IikoFail(IikoFailureKind.Permanent, "OrderId is required");

            var eventType = request.EventType?.Trim();
            if (!string.Equals(eventType, "OrderClosed", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(eventType, "OrderReturned", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("iiko order event ignored. EventType={EventType} OrderId={OrderId}", eventType, request.OrderId);
                return new IikoTransactionResponse { Success = true, Message = $"EventType '{eventType}' ignored" };
            }

            // Per-order lock: eyni sifariş paralel emal olunmasın (Hangfire 2 worker + retry job + plugin təkrar göndərişi).
            // İdempotency yoxlaması (marker tx) read-then-write olduğundan lock olmadan ikiqat xal mümkündür.
            // Hangfire.SqlServer bunu sp_getapplock ilə edir; storage konfiq olunmayıbsa (test) lock atlanır.
            JobStorage? storage = null;
            try { storage = JobStorage.Current; }
            catch (InvalidOperationException) { /* Hangfire konfiq olunmayıb */ }

            if (storage == null)
                return await ProcessIikoOrderEventCoreAsync(context, request, eventType!, ct);

            using var conn = storage.GetConnection();
            IDisposable orderLock;
            try
            {
                orderLock = conn.AcquireDistributedLock($"iiko-order:{request.OrderId.Trim()}", TimeSpan.FromSeconds(30));
            }
            catch (DistributedLockTimeoutException)
            {
                _logger.LogWarning("iiko order event: lock timeout — başqa worker emal edir. OrderId={OrderId}", request.OrderId);
                return IikoFail(IikoFailureKind.Transient, "Order is being processed by another worker");
            }

            using (orderLock)
            {
                return await ProcessIikoOrderEventCoreAsync(context, request, eventType!, ct);
            }
        }

        private async Task<IikoTransactionResponse> ProcessIikoOrderEventCoreAsync(
            WalletWebhookResolvedContext context, IikoOrderEventRequest request, string eventType, CancellationToken ct)
        {
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
            // iiko payload-dakı Customer.CardNumber = bizim LoyaltyCard.CardNumber və ya BarcodeValue.
            if (customerId == null && !string.IsNullOrWhiteSpace(request.CustomerCardNumber))
            {
                var byCard = await _loyaltyCardRepo.GetByCardNumberAsync(context.TenantId, request.CustomerCardNumber, ct)
                             ?? await _loyaltyCardRepo.GetByBarcodeValueAsync(context.TenantId, request.CustomerCardNumber, ct);
                customerId = byCard?.CustomerId;
            }
            // Fallback: iiko "kart nömrəsi" əslində müştərinin WalveroId-si ola bilər —
            // POS müştərini WalveroId ilə axtarır və sync WalveroId-ni iiko-ya kart kimi yazır.
            if (customerId == null && !string.IsNullOrWhiteSpace(request.CustomerCardNumber)
                && long.TryParse(request.CustomerCardNumber.Trim(), out var walveroId))
            {
                var byWalvero = await _customerRepo.GetByWalveroIdAsync(context.TenantId, walveroId, ct);
                customerId = byWalvero?.Id;
            }
            // Fallback: inteqrasiya "son N rəqəm" rejimindədirsə kassir qısa kodu yazıb → barkod sonluğu ilə tap.
            // 2+ uyğunluq → tapılmır (ambiguous), dəqiq barkod/telefon lazımdır.
            if (customerId == null && context.CardTrackMode == PosCardTrackMode.LastDigits
                && PosCardTrack.IsShortCode(request.CustomerCardNumber, context.CardTrackDigits))
            {
                var (byShort, matches) = await _loyaltyCardRepo.GetByBarcodeSuffixAsync(context.TenantId, request.CustomerCardNumber!.Trim(), ct);
                if (matches > 1)
                    return IikoFail(IikoFailureKind.Permanent, $"Ambiguous short card code {request.CustomerCardNumber} ({matches} cards)");
                customerId = byShort?.CustomerId;
            }
            if (customerId == null)
                return IikoFail(IikoFailureKind.Retriable, $"Customer not found (extId={request.ExternalCustomerId}, phone={request.CustomerPhone}, card={request.CustomerCardNumber})");

            // Müştərinin aktiv kartı (proqramından asılı olmayaraq — ambassador kartı ola bilər); qaydalar kartın proqramından
            var card = await _loyaltyCardRepo.GetActiveByCustomerAsync(context.TenantId, customerId.Value, ct);
            if (card == null)
                return IikoFail(IikoFailureKind.Retriable, "Active loyalty card not found");

            var program = await _loyaltyProgramRepo.GetByIdNameAsync(card.LoyaltyProgramId);
            var template = program?.CardTemplate;
            // ProgressBased + RewardAsPoints → PointsBased davranışı (xal rejimi)
            var templateType = template?.EffectiveType ?? CardTemplateType.PointsBased;
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
                return IikoFail(IikoFailureKind.Transient, "Loyalty card vanished mid-flight");

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

            EnqueueWalletPush(c.Id, program, -totalReverseDelta);

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

            // === Bonus istisnaları (super admin, PosEarnExclusions) ===
            // BlockCashback sətirləri məbləğ bonusunun bazasından çıxır (cashback %, xal/₼, xoş gəldin %);
            // BlockStamps sətirləri earn-rule döngüsündə sayılmır. İstisna yoxdursa cashbackBase == earnBase.
            var exclusionMap = await LoadExclusionMapAsync(context.TenantIntegrationId, ct);
            var earnLines = request.Items
                .Select(i => new EarnLine(i.ProductId, i.ProductNumber, i.CategoryId, i.ProductName, i.Amount, i.Sum))
                .ToList();
            var itemDiscounts = (request.Discounts ?? new List<IikoOrderDiscount>())
                .SelectMany(d => d.DiscountByItem ?? new List<IikoOrderDiscountByItem>())
                .Select(x => new EarnItemDiscount(x.ProductId, x.DiscountSum))
                .ToList();
            var exclusion = EarnExclusionMath.Compute(earnLines, itemDiscounts, request.TotalSum, walletRedeemSum,
                templateType == CardTemplateType.ProgressBased, exclusionMap);
            decimal cashbackBase = exclusion.CashbackBase;
            decimal stampSkippedUnits = 0m;

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
            {
                var activeRewards = await _rewardRepo.GetActiveRewardsByProgramIdAsync(card.LoyaltyProgramId, ct);
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
            var earnSource = program?.PosEarnSource ?? PosEarnSource.Legacy;

            // PosMirror: qazancı iiko hesablayır, balans iiko cüzdan güzgüsü ilə gəlir — burada earn yoxdur
            if (earnBase > 0 && PosEarnRouting.ComputeOrderEarn(earnSource))
            {
                var earnRules = await _earnRuleRepo.GetByTenantIntegrationIdAsync(context.TenantIntegrationId, ct);
                // Kartın proqramına aid qaydalar (proqram-spesifik > ümumi)
                var rulesByProduct = PosEarnRouting.RulesForProgram(earnRules, card.LoyaltyProgramId);

                var stampEarn = EarnExclusionMath.ApplyEarnRules(
                    earnLines,
                    line =>
                    {
                        // Earn rule ProductId (GUID) və ya ProductNumber (iiko nomenklatura kodu) ilə uyğunlaşa bilər.
                        // iiko loyalty proqramları məhsulları kod (ProductNumber) ilə saxlayır.
                        var pid = (line.ProductId ?? "").Trim();
                        var pnum = (line.ProductNumber ?? "").Trim();
                        PosProductEarnRule? rule = null;
                        if (pid.Length > 0)
                            rulesByProduct.TryGetValue(pid, out rule);
                        if (rule == null && pnum.Length > 0)
                            rulesByProduct.TryGetValue(pnum, out rule);
                        return rule == null ? null : new EarnRuleHit(rule.StepsPerUnit, rule.PointsPerUnit);
                    },
                    line =>
                    {
                        // Redeem açarı ProductNumber (varsa) və ya GUID ola bilər — hər ikisini yoxla.
                        // Loyalty discount-da iştirak edən item earn-dən düşür.
                        var pid = (line.ProductId ?? "").Trim();
                        var pnum = (line.ProductNumber ?? "").Trim();
                        return (pid.Length > 0 && redeemedUnitsByProduct.ContainsKey(pid)) ||
                               (pnum.Length > 0 && redeemedUnitsByProduct.ContainsKey(pnum));
                    },
                    exclusionMap);
                anyEarnRuleMatched = stampEarn.AnyRuleMatched;
                totalEarnSteps = stampEarn.Steps;
                totalEarnPoints = stampEarn.Points;
                stampSkippedUnits = stampEarn.SkippedUnits;

                // Xal rejimi (hədiyyə template + RewardAsPoints): earn rule-lar StepsPerUnit ilə qurulub
                // (1 məhsul = 1 addım). PointsPerUnit boşdursa addımları xal kimi say → CurrentPoints += N.
                if (template?.RewardAsPoints == true && totalEarnPoints == 0m && totalEarnSteps > 0)
                {
                    totalEarnPoints = totalEarnSteps;
                    totalEarnSteps = 0;
                }

                // Möhür proqramı: qayda uyğun gəlməyibsə sifariş başına möhür (StepsPerOrder)
                var orderSteps = PosEarnRouting.StepsForOrder(program?.StepsPerOrder, anyEarnRuleMatched, templateType);
                if (orderSteps > 0)
                {
                    totalEarnSteps = orderSteps;
                    anyEarnRuleMatched = true; // faiz fallback-ı açılmasın
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
                        totalEarnPoints = cashbackBase * effPct.Value / 100m;
                    else if (program.PointsPerCurrency > 0)
                        totalEarnPoints = cashbackBase * program.PointsPerCurrency;
                }
            }

            if (exclusion.AnyBlocked || stampSkippedUnits > 0m)
            {
                _logger.LogInformation(
                    "iiko OrderClosed: earn exclusions applied. OrderId={OrderId} TenantId={TenantId} EarnBase={EarnBase} CashbackBase={CashbackBase} Excluded={Excluded} StampSkippedUnits={StampSkipped} Lines={Lines}",
                    request.OrderId, context.TenantId, earnBase, cashbackBase, exclusion.ExcludedNet, stampSkippedUnits,
                    string.Join("; ", exclusion.BlockedLines.Select(l =>
                        $"{l.Name ?? l.ProductId} x{l.Amount:0.##} net={l.Net:0.##} [{(l.Block.BlockCashback ? "cashback" : "")}{(l.Block.BlockCashback && l.Block.BlockStamps ? "+" : "")}{(l.Block.BlockStamps ? "stamps" : "")}] {l.Block.SourceKind}:{l.Block.SourceName}")));
            }

            var c = await _uow.Repository<LoyaltyCard>().GetByIdAsync(card.Id, ct);
            if (c == null)
                return IikoFail(IikoFailureKind.Transient, "Loyalty card vanished mid-flight");

            decimal earnedValue = 0m;
            decimal welcomeBonus = 0m;
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
                    Description = "iiko OrderClosed earn" + EarnExclusionMath.DescribeSuffix(exclusion, stampSkippedUnits),
                    BranchId = context.BranchId,
                    RegUserId = 1,
                    RegDate = DateTime.UtcNow,
                    IsActive = true,
                    BalanceAfter = c.CurrentPoints,
                    LocalOccurredAt = DateTimeOffset.UtcNow,
                    TimeZoneIdUsed = "UTC"
                };
                _uow.Repository<Transaction>().Add(earnTx);

                if (program != null)
                {
                    // % hissəsi bonus düşməyən məhsullardan hesablanmasın → cashbackBase (istisna yoxdursa = earnBase)
                    welcomeBonus = await _welcomeBonus.TryGrantFirstPurchaseAsync(c, program, templateType, totalStepsPerCycle,
                        cashbackBase, totalEarnPoints, context.BranchId, idempotencyKey + ":welcome", ct);
                    if (welcomeBonus > 0) c.LifetimePoints += welcomeBonus;
                }
            }

            // ProgressBased-də manat→step blind conversion mənasız. Reward konsum yalnız Discounts.DiscountByItem yolu ilə.
            if (walletRedeemSum > 0 && templateType != CardTemplateType.ProgressBased && !PosEarnRouting.ApplyOrderWalletPayment(earnSource))
            {
                _logger.LogInformation(
                    "iiko OrderClosed: walletRedeemSum={Sum} skipped — program PosMirror (balance comes from iiko wallet mirror). OrderId={OrderId} CardId={CardId}",
                    walletRedeemSum, request.OrderId, c.Id);
            }
            else if (walletRedeemSum > 0 && templateType != CardTemplateType.ProgressBased)
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

            bool walletRedeemApplied = walletRedeemSum > 0 && templateType != CardTemplateType.ProgressBased
                                       && PosEarnRouting.ApplyOrderWalletPayment(earnSource);
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
            decimal netDelta = earnedValue + welcomeBonus - effectiveWalletRedeem - discountRedeemPoints;
            EnqueueWalletPush(c.Id, program, netDelta);
            // WalveroRules: qazancı Walvero hesablayır → iiko cüzdanı yeni balansla sinxron qalsın
            // (öz topup-umuzun güzgü əks-sədası WalveroRules-da balansa toxunmur)
            var pushToPos = PosEarnRouting.PushBalanceAfterOrderEarn(earnSource)
                            && templateType != CardTemplateType.ProgressBased && netDelta != 0;
            if (welcomeBonus > 0 || pushToPos)
                await _welcomeBonus.SyncPosBalanceSafeAsync(context.TenantId, c.CustomerId, c.CurrentPoints, ct);

            string msg = templateType switch
            {
                CardTemplateType.ProgressBased => $"Steps: {c.CurrentSteps}, FreeCoffees: {c.CompletedCycles}",
                CardTemplateType.TierBased => $"Earned: {earnedValue}, Redeemed: {walletRedeemSum}, Balance: {c.CurrentPoints}, Tier: {(template != null ? ResolveTier(c.LifetimePoints, template).tierName : "-")}",
                _ => $"Earned: {earnedValue}, Redeemed: {walletRedeemSum}, Balance: {c.CurrentPoints}"
            };

            return new IikoTransactionResponse { Success = true, Message = msg };
        }

        /// <summary>
        /// Wallet pass push-u Hangfire background-a atır (webhook cavabı gözlətməsin → 499 fix).
        /// program null-dursa push mənasızdır (skip).
        /// </summary>
        private void EnqueueWalletPush(int cardId, LoyaltyProgram? program, decimal delta)
        {
            if (program == null)
            {
                _logger.LogDebug("Wallet push skipped: program null. CardId={CardId}", cardId);
                return;
            }
            BackgroundJob.Enqueue<IWalletPushJob>(j => j.RunAsync(cardId, program.Id, delta));
        }
    }
}
