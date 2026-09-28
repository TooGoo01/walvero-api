using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class WelcomeBonusService : IWelcomeBonusService
    {
        public const string FirstPurchaseDescription = "İlk sifariş bonusu";
        public const string SignupGiftDescription = "Xoş gəldin hədiyyəsi";

        private readonly LoyaltyWalletDbContext _db;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<WelcomeBonusService> _logger;

        public WelcomeBonusService(LoyaltyWalletDbContext db, IServiceScopeFactory scopeFactory, ILogger<WelcomeBonusService> logger)
        {
            _db = db;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<decimal> TryGrantFirstPurchaseAsync(
            LoyaltyCard card, LoyaltyProgram program, CardTemplateType templateType, int totalSteps,
            decimal? orderAmount, decimal regularEarnPoints, int? branchId, string? orderRef, CancellationToken ct = default)
        {
            if (!program.FirstPurchaseBonusEnabled || card.FirstPurchaseBonusGrantedAt != null)
                return 0m;

            // Cari earn tx hələ save olunmayıb → DB sorğusu yalnız əvvəlki qazancları görür.
            var hasPriorEarn = await _db.Set<Transaction>().AnyAsync(
                t => t.LoyaltyCardId == card.Id && t.IsActive && t.Status == RedeemStatus.Applied
                     && (t.Type == TransactionType.Earned || t.Type == TransactionType.Bonus), ct);
            if (hasPriorEarn)
            {
                card.FirstPurchaseBonusGrantedAt = DateTime.UtcNow;
                return 0m;
            }

            var isPercent = program.FirstPurchaseBonusType == WelcomeBonusType.Percent;
            if (isPercent && (orderAmount is null || orderAmount <= 0))
            {
                // Məbləğ yoxdursa flag qoyulmur — növbəti məbləğli sifarişdə verilsin
                _logger.LogInformation("Welcome bonus skipped: percent type without amount. CardId={CardId}", card.Id);
                return 0m;
            }

            var bonus = CalculateFirstPurchaseBonus(
                program.FirstPurchaseBonusType, program.FirstPurchaseBonusValue, program.FirstPurchaseBonusMaxPoints,
                templateType, orderAmount, regularEarnPoints);

            card.FirstPurchaseBonusGrantedAt = DateTime.UtcNow;
            if (bonus <= 0)
            {
                _logger.LogInformation("Welcome bonus not granted: regular earn already >= target. CardId={CardId}, Regular={Regular}, Amount={Amount}",
                    card.Id, regularEarnPoints, orderAmount);
                return 0m;
            }

            Apply(card, templateType, totalSteps, bonus);
            AddBonusTx(card, bonus, orderAmount, branchId, orderRef, FirstPurchaseDescription);

            _logger.LogInformation("Welcome bonus granted. CardId={CardId}, Bonus={Bonus}, Regular={Regular}, Amount={Amount}",
                card.Id, bonus, regularEarnPoints, orderAmount);
            return bonus;
        }

        /// <summary>
        /// İlk sifariş bonusunun xalis dəyəri (pure, test üçün).
        /// Percent + PointsBased: hədəf = sifarişin X%-i; bonus = hədəf − adi cashback ("cəmi X%").
        /// Percent + ProgressBased: floor(sifarişin X%-i) addım, çıxılma yoxdur (adi qazanc ştampdır).
        /// Fixed: dəyər olduğu kimi, əlavə olaraq. MaxPoints tavanı yalnız Percent-də, əlavə (top-up) hissəsinə.
        /// </summary>
        public static decimal CalculateFirstPurchaseBonus(
            WelcomeBonusType type, decimal value, decimal? maxPoints,
            CardTemplateType templateType, decimal? orderAmount, decimal regularEarnPoints)
        {
            decimal bonus;
            if (type == WelcomeBonusType.Percent)
            {
                if (orderAmount is null || orderAmount <= 0) return 0m;
                var target = Math.Round(orderAmount.Value * value / 100m, 2);
                bonus = templateType == CardTemplateType.ProgressBased
                    ? Math.Floor(target)
                    : Math.Round(target - Math.Max(0m, regularEarnPoints), 2);
                if (maxPoints is > 0)
                    bonus = Math.Min(bonus, maxPoints.Value);
            }
            else
            {
                bonus = value;
                if (templateType == CardTemplateType.ProgressBased)
                    bonus = Math.Floor(bonus);
            }
            return bonus;
        }

        public async Task<decimal> TryGrantSignupGiftAsync(
            LoyaltyCard card, LoyaltyProgram program, CardTemplateType templateType, int totalSteps,
            CancellationToken ct = default)
        {
            if (!program.SignupGiftEnabled || program.SignupGiftValue <= 0 || card.SignupGiftGrantedAt != null)
                return 0m;

            // Tarixçəsi olan (reaktiv / ambassador) kart hədiyyə almır
            var hasAnyTx = await _db.Set<Transaction>().AnyAsync(t => t.LoyaltyCardId == card.Id, ct);
            card.SignupGiftGrantedAt = DateTime.UtcNow;
            if (hasAnyTx) return 0m;

            decimal gift = program.SignupGiftValue;
            if (templateType == CardTemplateType.ProgressBased && program.SignupGiftType == SignupGiftType.FreeItem)
                gift = program.SignupGiftValue * Math.Max(1, totalSteps);

            Apply(card, templateType, totalSteps, gift);
            AddBonusTx(card, gift, null, null, null, SignupGiftDescription);

            _logger.LogInformation("Signup gift granted. CardId={CardId}, Gift={Gift}, Type={Type}", card.Id, gift, program.SignupGiftType);
            return gift;
        }

        public async Task SyncPosBalanceSafeAsync(int tenantId, int customerId, decimal currentPoints, CancellationToken ct = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IPosSyncService>();
                await sync.SyncBalanceToPosAsync(tenantId, customerId, currentPoints, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Welcome bonus POS balance sync failed. CustomerId={CustomerId}, TenantId={TenantId}", customerId, tenantId);
            }
        }

        private void Apply(LoyaltyCard card, CardTemplateType templateType, int totalSteps, decimal delta)
        {
            BalanceMath.ApplyDelta(card, new CardTemplateSnapshot { TemplateType = templateType, TotalSteps = totalSteps }, delta);
            card.EditDate = DateTime.UtcNow;
            _db.Set<LoyaltyCard>().Update(card);
        }

        private void AddBonusTx(LoyaltyCard card, decimal points, decimal? amount, int? branchId, string? orderRef, string description)
        {
            _db.Set<Transaction>().Add(new Transaction
            {
                LoyaltyCardId = card.Id,
                Type = TransactionType.Bonus,
                Status = RedeemStatus.Applied,
                Points = points,
                Amount = amount,
                BalanceAfter = card.CurrentPoints,
                Description = description,
                BranchId = branchId,
                OrderId = orderRef,
                RegDate = DateTime.UtcNow,
                IsActive = true,
                LocalOccurredAt = DateTimeOffset.UtcNow,
                TimeZoneIdUsed = "UTC"
            });
        }
    }
}
