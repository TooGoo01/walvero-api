using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class TransactionReverseService : ITransactionReverseService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ILoyaltyCardRepository _loyaltyCardRepository;
        private readonly ILoyaltyProgramRepository _loyaltyProgramRepository;
        private readonly ITenantBrandingRepository _tenantBrandingRepository;
        private readonly IWalletRouter _walletRouter;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly ILogger<TransactionReverseService> _logger;

        public TransactionReverseService(
            IUnitOfWork unitOfWork,
            ITransactionRepository transactionRepository,
            ILoyaltyCardRepository loyaltyCardRepository,
            ILoyaltyProgramRepository loyaltyProgramRepository,
            ITenantBrandingRepository tenantBrandingRepository,
            IWalletRouter walletRouter,
            LoyaltyWalletDbContext dbContext,
            ILogger<TransactionReverseService> logger)
        {
            _unitOfWork = unitOfWork;
            _transactionRepository = transactionRepository;
            _loyaltyCardRepository = loyaltyCardRepository;
            _loyaltyProgramRepository = loyaltyProgramRepository;
            _tenantBrandingRepository = tenantBrandingRepository;
            _walletRouter = walletRouter;
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<ServiceResult<ReverseTransactionResponseDto>> ReverseTransactionAsync(
            ReverseTransactionDto dto,
            CancellationToken ct = default)
        {
            try
            {
                // Idempotency yalnız OrderId varsa və TransactionId üçün artıq Adjusted transaction yaradılmışsa
                if (!string.IsNullOrWhiteSpace(dto.OrderId))
                {
                    // dto.OrderId → ReverseLastTransactionByCardAsync-də original.OrderId saxlanır.
                    // Eyni OrderId üçün artıq Adjusted varsa, deməli bu reverse artıq tətbiq olunub.
                    // Lakin original transaction-ın Status-u Reversed olmalıdır — yoxsa stale data deməkdir.
                    var existingReverse = await _dbContext.Set<Transaction>()
                        .FirstOrDefaultAsync(
                            t => t.OrderId == dto.OrderId &&
                                 t.Type == TransactionType.Adjusted,
                            ct);

                    if (existingReverse != null)
                    {
                        // Yalnız original artıq Reversed-dirsə qoru
                        var orig = await _transactionRepository.GetByIdAsync(dto.TransactionId, ct);
                        if (orig != null && orig.Status == RedeemStatus.Reversed)
                        {
                            var card1 = await _loyaltyCardRepository.GetById(existingReverse.LoyaltyCardId, ct);
                            if (card1 != null)
                            {
                                return ServiceResult<ReverseTransactionResponseDto>.Ok(
                                    new ReverseTransactionResponseDto
                                    {
                                        Status = "ALREADY_REVERSED",
                                        ReversedPoints = 0,
                                        UpdatedBalance = card1.CurrentPoints
                                    },
                                    "Transaction already reversed.");
                            }
                        }
                        // Stale Adjusted (original Status=Applied qalıb) — davam et və düzgün reverse-i tətbiq et
                        _logger.LogWarning("Stale Adjusted transaction found for OrderId={OrderId}. Re-applying reverse.", dto.OrderId);
                    }
                }

                // Find the original transaction
                var originalTransaction = await _transactionRepository.GetByIdAsync(dto.TransactionId, ct);
                if (originalTransaction == null)
                {
                    return ServiceResult<ReverseTransactionResponseDto>.Fail("Original transaction not found.");
                }

                // Validate original type
                TransactionType expectedType;
                if (dto.OriginalType.ToUpper() == "EARN")
                {
                    expectedType = TransactionType.Earned;
                }
                else if (dto.OriginalType.ToUpper() == "REDEEM")
                {
                    expectedType = TransactionType.Redeemed;
                }
                else
                {
                    return ServiceResult<ReverseTransactionResponseDto>.Fail("Invalid OriginalType. Must be EARN or REDEEM.");
                }

                if (originalTransaction.Type != expectedType)
                {
                    return ServiceResult<ReverseTransactionResponseDto>.Fail(
                        $"Transaction type mismatch. Expected {expectedType}, but found {originalTransaction.Type}.");
                }

                // Get the loyalty card with tracking
                var cardRepo = _unitOfWork.Repository<LoyaltyCard>();
                var card = await cardRepo.GetByIdAsync(originalTransaction.LoyaltyCardId, ct);
                if (card == null)
                {
                    return ServiceResult<ReverseTransactionResponseDto>.Fail("Loyalty card not found.");
                }

                // Original delta: Transaction.Points həmişə Math.Abs(delta) saxlayır,
                // işarə isə Type-a görə bilinir (Earned = +, Redeemed = -).
                // Bu yanaşma BalanceAfter-dən asılı deyil və köhnə data üçün də etibarlıdır.
                decimal originalDelta = originalTransaction.Type == TransactionType.Earned
                    ? Math.Abs(originalTransaction.Points)
                    : -Math.Abs(originalTransaction.Points);

                // Fallback: əgər Points 0-dırsa, BalanceAfter fərqindən hesabla
                if (originalDelta == 0m)
                {
                    var previousTransaction = await _dbContext.Set<Transaction>()
                        .Where(t => t.LoyaltyCardId == originalTransaction.LoyaltyCardId &&
                                   t.Id < originalTransaction.Id)
                        .OrderByDescending(t => t.Id)
                        .FirstOrDefaultAsync(ct);

                    originalDelta = previousTransaction != null
                        ? originalTransaction.BalanceAfter - previousTransaction.BalanceAfter
                        : originalTransaction.BalanceAfter;
                }

                // Hələ də 0-dırsa — reverse mənasız, error qaytar
                if (originalDelta == 0m)
                {
                    _logger.LogWarning("Reverse skipped: originalDelta=0. TransactionId={TransactionId}", originalTransaction.Id);
                    return ServiceResult<ReverseTransactionResponseDto>.Fail(
                        "Original transaction-ın delta-sı 0-dır, reverse mənasız.");
                }

                // Calculate reverse delta (opposite of original)
                decimal reverseDelta = -originalDelta;

                // Get card template snapshot for applying delta
                var program = await _dbContext.Set<LoyaltyProgram>()
                    .Include(p => p.CardTemplate)
                    .FirstOrDefaultAsync(p => p.Id == card.LoyaltyProgramId, ct);
                if (program == null)
                {
                    return ServiceResult<ReverseTransactionResponseDto>.Fail("Loyalty program not found.");
                }
                if (program.CardTemplate == null)
                {
                    return ServiceResult<ReverseTransactionResponseDto>.Fail("Card template not found for this program.");
                }

                var branding = await _tenantBrandingRepository.GetByTenantIdAsync(card.TenantId, ct);
                var snapshot = Helpers.CardTemplateSnapshotFactory.From(
                    program,
                    program.CardTemplate,
                    branding,
                    (url) => url);

                // Store before values
                var beforePoints = card.CurrentPoints;
                var beforeSteps = card.CurrentSteps;
                var beforeCompletedCycles = card.CompletedCycles;

                // Apply reverse delta (opposite of original)
                ApplyReverseDelta(card, snapshot, reverseDelta, originalTransaction.Type);

                // Calculate reversed amount/points (decimal — kəsr saxlanır)
                decimal reversedPoints = Math.Abs(reverseDelta);
                decimal? reversedAmount = originalTransaction.Amount;

                // Create reverse transaction
                var reverseTransaction = new Transaction
                {
                    LoyaltyCardId = card.Id,
                    Type = TransactionType.Adjusted,
                    Status = RedeemStatus.Applied,
                    Points = card.CurrentPoints,
                    BalanceAfter = card.CurrentPoints,
                    Amount = reversedAmount,
                    ReferenceNumber = card.BarcodeValue,
                    OrderId = dto.OrderId,
                    LocalOccurredAt = DateTimeOffset.UtcNow,
                    TimeZoneIdUsed = originalTransaction.TimeZoneIdUsed,
                    Description = $"Transaction reversed: {dto.Reason}. Original: {originalTransaction.Description}"
                };

                // Original transaction-ın status-unu Reversed et — eyni transaction yenidən reverse olunmasın
                originalTransaction.Status = RedeemStatus.Reversed;

                // Update card and save
                cardRepo.Update(card);
                var txRepo = _unitOfWork.Repository<Transaction>();
                txRepo.Update(originalTransaction);
                txRepo.Add(reverseTransaction);
                _unitOfWork.Commit();

                _logger.LogInformation(
                    "Transaction reversed. TransactionId={TransactionId}, CardId={CardId}, " +
                    "Points {Before}->{After}, Steps {StepsBefore}->{StepsAfter}, " +
                    "CompletedCycles {CyclesBefore}->{CyclesAfter}, ReverseDelta={ReverseDelta}",
                    dto.TransactionId, card.Id, beforePoints, card.CurrentPoints,
                    beforeSteps, card.CurrentSteps, beforeCompletedCycles, card.CompletedCycles, reverseDelta);

                // Update wallet passes
                try
                {
                    var primaryPlatform = card.IsIOS ? WalletPlatform.AppleWallet : WalletPlatform.GooglePay;
                    await _walletRouter.RouteAsync(
                        primaryPlatform,
                        card.Id,
                        snapshot,
                        card.CurrentPoints,
                        card.CurrentSteps,
                        card.CompletedCycles,
                        (int)reverseDelta,
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update wallet pass after reverse. CardId={CardId}", card.Id);
                    // Don't fail the whole operation if wallet update fails
                }

                var response = new ReverseTransactionResponseDto
                {
                    Status = "SUCCESS",
                    ReversedAmount = reversedAmount,
                    ReversedPoints = reversedPoints,
                    UpdatedBalance = card.CurrentPoints
                };

                return ServiceResult<ReverseTransactionResponseDto>.Ok(response, "Transaction reversed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reverse transaction. TransactionId={TransactionId}", dto.TransactionId);
                return ServiceResult<ReverseTransactionResponseDto>.Fail($"Failed to reverse transaction: {ex.Message}");
            }
        }

        public async Task<ServiceResult<ReverseTransactionResponseDto>> ReverseLastTransactionByCardAsync(
            string cardNumber, string? reason, CancellationToken ct = default)
        {
            try
            {
                // 1. cardNumber ilə LoyaltyCard tap
                var card = await _dbContext.Set<LoyaltyCard>()
                    .FirstOrDefaultAsync(c => c.BarcodeValue == cardNumber && c.IsActive, ct);

                if (card == null)
                {
                    // CardNumber ilə də axtar
                    card = await _dbContext.Set<LoyaltyCard>()
                        .FirstOrDefaultAsync(c => c.CardNumber == cardNumber && c.IsActive, ct);
                }

                if (card == null)
                    return ServiceResult<ReverseTransactionResponseDto>.Fail("Loyalty card not found.");

                // 2. Son aktiv transaction-ı tap (Earned və ya Redeemed, status=Active)
                var lastTransaction = await _dbContext.Set<Transaction>()
                    .Where(t => t.LoyaltyCardId == card.Id
                        && (t.Type == TransactionType.Earned || t.Type == TransactionType.Redeemed)
                        && t.Status == RedeemStatus.Applied)
                    .OrderByDescending(t => t.Id)
                    .FirstOrDefaultAsync(ct);

                if (lastTransaction == null)
                    return ServiceResult<ReverseTransactionResponseDto>.Fail("No reversible transaction found.");

                // 3. Mövcud ReverseTransactionAsync çağır
                var dto = new ReverseTransactionDto
                {
                    TransactionId = lastTransaction.Id,
                    OrderId = lastTransaction.OrderId ?? $"reverse-{lastTransaction.Id}",
                    OriginalType = lastTransaction.Type == TransactionType.Earned ? "EARN" : "REDEEM",
                    Reason = string.IsNullOrEmpty(reason) ? "Admin reverse" : reason,
                    Timestamp = DateTime.UtcNow
                };

                return await ReverseTransactionAsync(dto, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reverse last transaction by card. CardNumber={CardNumber}", cardNumber);
                return ServiceResult<ReverseTransactionResponseDto>.Fail($"Failed: {ex.Message}");
            }
        }

        private void ApplyReverseDelta(LoyaltyCard card, CardTemplateSnapshot snapshot, decimal reverseDelta, TransactionType originalType)
        {
            // Reverse delta is opposite of original
            // If original was EARN (positive), reverse is negative
            // If original was REDEEM (negative), reverse is positive
            // Use the same ApplyDelta logic but with reverseDelta

            switch (snapshot.TemplateType)
            {
                case CardTemplateType.ProgressBased:
                    // ProgressBased: steps integer — kəsr atılır
                    int totalSteps = Math.Max(1, snapshot.TotalSteps);
                    int reverseStepsDelta = (int)reverseDelta;

                    if (reverseStepsDelta > 0) // Adding points back (reversing a redeem)
                    {
                        int newSteps = card.CurrentSteps + reverseStepsDelta;
                        card.CurrentPoints = Math.Max(0, card.CurrentPoints + reverseStepsDelta);

                        // Calculate if we complete any cycles
                        int cyclesEarned = newSteps / totalSteps;
                        if (cyclesEarned > 0)
                        {
                            card.CompletedCycles += cyclesEarned;
                        }

                        card.CurrentSteps = newSteps % totalSteps;
                    }
                    else if (reverseStepsDelta < 0) // Subtracting points (reversing an earn)
                    {
                        // Subtract steps
                        int newSteps = card.CurrentSteps + reverseStepsDelta; // reverseStepsDelta is negative
                        card.CurrentPoints = Math.Max(0, card.CurrentPoints + reverseStepsDelta);

                        // If we go negative, reduce completed cycles
                        if (newSteps < 0)
                        {
                            int cyclesToReduce = (Math.Abs(newSteps) / totalSteps) + 1;
                            card.CompletedCycles = Math.Max(0, card.CompletedCycles - cyclesToReduce);
                            newSteps = totalSteps + (newSteps % totalSteps);
                        }

                        card.CurrentSteps = Math.Max(0, newSteps);
                    }
                    break;

                case CardTemplateType.PointsBased:
                case CardTemplateType.TierBased:
                    // PointsBased/TierBased: kəsr saxlanır
                    card.CurrentPoints = Math.Max(0m, card.CurrentPoints + reverseDelta);
                    break;
            }
        }

        public async Task<ServiceResult<EditAmountResponseDto>> EditTransactionAmountAsync(
            int transactionId, decimal newAmount, string? reason, int? callerTenantId, CancellationToken ct = default)
        {
            try
            {
                if (newAmount <= 0)
                    return ServiceResult<EditAmountResponseDto>.Fail("NewAmount must be positive.");

                var tx = await _transactionRepository.GetByIdAsync(transactionId, ct);
                if (tx == null)
                    return ServiceResult<EditAmountResponseDto>.Fail("Transaction not found.");

                // Earn tranzaksiyaları: Applied (balans düzəlişi ilə) və ya
                // FlaggedForReview (təsdiqdən əvvəl düzəliş — balans hələ tətbiq olunmayıb)
                var isFlagged = tx.Status == RedeemStatus.FlaggedForReview;
                if (tx.Type != TransactionType.Earned
                    || (tx.Status != RedeemStatus.Applied && !isFlagged))
                    return ServiceResult<EditAmountResponseDto>.Fail(
                        "Yalnız Applied və ya FlaggedForReview statuslu Earn tranzaksiyalarının məbləği düzəldilə bilər.");

                var cardRepo = _unitOfWork.Repository<LoyaltyCard>();
                var card = await cardRepo.GetByIdAsync(tx.LoyaltyCardId, ct);
                if (card == null)
                    return ServiceResult<EditAmountResponseDto>.Fail("Loyalty card not found.");

                // Tenant admin yalnız öz tenant-ının tranzaksiyasını düzəldə bilər (null = Super Admin)
                if (callerTenantId.HasValue && card.TenantId != callerTenantId.Value)
                    return ServiceResult<EditAmountResponseDto>.Fail("Bu əməliyyat sizin tenant-a aid deyil.");

                var program = await _dbContext.Set<LoyaltyProgram>()
                    .Include(p => p.CardTemplate)
                    .FirstOrDefaultAsync(p => p.Id == card.LoyaltyProgramId, ct);
                if (program == null)
                    return ServiceResult<EditAmountResponseDto>.Fail("Loyalty program not found.");
                if (program.CardTemplate == null)
                    return ServiceResult<EditAmountResponseDto>.Fail("Card template not found for this program.");

                var branding = await _tenantBrandingRepository.GetByTenantIdAsync(card.TenantId, ct);
                var snapshot = Helpers.CardTemplateSnapshotFactory.From(
                    program,
                    program.CardTemplate,
                    branding,
                    (url) => url);

                var oldAmount = tx.Amount;
                var oldPoints = tx.Points;
                decimal newPoints = oldPoints;
                decimal delta = 0m;

                // ProgressBased: xal = tam addım sayı, proporsional kəsr mənasızdır — yalnız məbləğ dəyişir.
                // Köhnə məbləğ/xal 0 və ya null-dursa nisbət qurmaq mümkün deyil — yalnız məbləğ dəyişir.
                if (snapshot.TemplateType != CardTemplateType.ProgressBased
                    && oldAmount.HasValue && oldAmount.Value != 0m
                    && oldPoints != 0m)
                {
                    // Proporsional: kəsr saxlanır, rounding yoxdur (mövcud earn axını ilə eyni)
                    newPoints = oldPoints * newAmount / oldAmount.Value;
                    delta = newPoints - oldPoints;
                }

                var beforePoints = card.CurrentPoints;
                // Flagged tx-in balansı hələ tətbiq olunmayıb — yalnız Amount/Points dəyişir,
                // balans ApproveFlagged zamanı yeni Points ilə tətbiq olunacaq.
                if (delta != 0m && !isFlagged)
                    BalanceMath.ApplyDelta(card, snapshot, delta);

                tx.Amount = newAmount;
                tx.Points = Math.Abs(newPoints);
                // Bu tx-in tarixi balans izi düzəlir; sonrakı tranzaksiyaların BalanceAfter-i
                // tarixən köhnəlmiş qalır — qəbul edilən kompromis.
                if (!isFlagged)
                    tx.BalanceAfter += delta;
                tx.Description =
                    $"{tx.Description} (Məbləğ düzəlişi: {(oldAmount.HasValue ? oldAmount.Value.ToString("0.##") : "-")}→{newAmount:0.##}" +
                    $"{(string.IsNullOrWhiteSpace(reason) ? "" : $". Səbəb: {reason}")})";

                // LifetimePoints və lottery sayğacları qəsdən toxunulmur — düzəliş yeni alış deyil.
                cardRepo.Update(card);
                _unitOfWork.Repository<Transaction>().Update(tx);
                _unitOfWork.Commit();

                _logger.LogInformation(
                    "Transaction amount edited. TxId={TxId}, CardId={CardId}, Amount {OldAmount}->{NewAmount}, " +
                    "Points {OldPoints}->{NewPoints}, Balance {BeforeBalance}->{AfterBalance}",
                    tx.Id, card.Id, oldAmount, newAmount, oldPoints, newPoints, beforePoints, card.CurrentPoints);

                if (delta != 0m && !isFlagged)
                {
                    try
                    {
                        var primaryPlatform = card.IsIOS ? WalletPlatform.AppleWallet : WalletPlatform.GooglePay;
                        await _walletRouter.RouteAsync(
                            primaryPlatform,
                            card.Id,
                            snapshot,
                            card.CurrentPoints,
                            card.CurrentSteps,
                            card.CompletedCycles,
                            (int)delta,
                            ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update wallet pass after amount edit. CardId={CardId}", card.Id);
                    }
                }

                return ServiceResult<EditAmountResponseDto>.Ok(new EditAmountResponseDto
                {
                    OldAmount = oldAmount,
                    NewAmount = newAmount,
                    OldPoints = oldPoints,
                    NewPoints = tx.Points,
                    PointsDelta = delta,
                    UpdatedBalance = card.CurrentPoints
                }, "Amount updated.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to edit transaction amount. TransactionId={TransactionId}", transactionId);
                return ServiceResult<EditAmountResponseDto>.Fail($"Failed to edit transaction amount: {ex.Message}");
            }
        }

        public async Task<ServiceResult> ApproveFlaggedAsync(int transactionId, int? callerBranchId, CancellationToken ct = default)
        {
            try
            {
                var tx = await _dbContext.Set<Transaction>()
                    .FirstOrDefaultAsync(t => t.Id == transactionId, ct);

                if (tx is null)
                    return new ServiceResult(false, "Transaction not found.");

                if (tx.Status != RedeemStatus.FlaggedForReview)
                    return new ServiceResult(false, "Yalnız FlaggedForReview statuslu əməliyyatlar təsdiqlənə bilər.");

                // Branch-scoped admin yalnız öz filialının tx-ini təsdiqləyə bilər
                if (callerBranchId.HasValue && tx.BranchId != callerBranchId.Value)
                    return new ServiceResult(false, "Bu əməliyyat sizin filialınıza aid deyil.");

                var card = await _loyaltyCardRepository.GetById(tx.LoyaltyCardId, ct);
                if (card is null)
                    return new ServiceResult(false, "Card not found.");

                // Balansa indi tətbiq et (redeem zamanı atılmışdı)
                var signedDelta = tx.Type == TransactionType.Redeemed ? -tx.Points : tx.Points;
                card.CurrentPoints = Math.Max(0m, card.CurrentPoints + signedDelta);

                tx.Status = RedeemStatus.Applied;
                tx.BalanceAfter = card.CurrentPoints;

                _unitOfWork.Repository<LoyaltyCard>().Update(card);
                _unitOfWork.Repository<Transaction>().Update(tx);
                _unitOfWork.Commit();

                _logger.LogInformation("Flagged tx approved. TxId={TxId}, CardId={CardId}, Delta={Delta}",
                    tx.Id, card.Id, signedDelta);

                return new ServiceResult(true, "Təsdiqləndi və balansa tətbiq olundu.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApproveFlaggedAsync failed. TxId={TxId}", transactionId);
                return new ServiceResult(false, "Sistem xətası baş verdi.");
            }
        }

        public async Task<ServiceResult> RejectFlaggedAsync(int transactionId, int? callerBranchId, string? reason, CancellationToken ct = default)
        {
            try
            {
                var tx = await _dbContext.Set<Transaction>()
                    .FirstOrDefaultAsync(t => t.Id == transactionId, ct);

                if (tx is null)
                    return new ServiceResult(false, "Transaction not found.");

                if (tx.Status != RedeemStatus.FlaggedForReview)
                    return new ServiceResult(false, "Yalnız FlaggedForReview statuslu əməliyyatlar rədd edilə bilər.");

                if (callerBranchId.HasValue && tx.BranchId != callerBranchId.Value)
                    return new ServiceResult(false, "Bu əməliyyat sizin filialınıza aid deyil.");

                tx.Status = RedeemStatus.Reversed;
                tx.ReverseReason = string.IsNullOrWhiteSpace(reason) ? "Admin rejected suspicious tx" : reason;

                _unitOfWork.Repository<Transaction>().Update(tx);
                _unitOfWork.Commit();

                _logger.LogInformation("Flagged tx rejected. TxId={TxId}, Reason={Reason}", tx.Id, tx.ReverseReason);
                return new ServiceResult(true, "Rədd edildi.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RejectFlaggedAsync failed. TxId={TxId}", transactionId);
                return new ServiceResult(false, "Sistem xətası baş verdi.");
            }
        }
    }
}

