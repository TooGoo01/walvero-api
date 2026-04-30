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
                // Check idempotency - if this reverse was already processed
                var existingReverse = await _dbContext.Set<Transaction>()
                    .FirstOrDefaultAsync(
                        t => t.OrderId == dto.OrderId &&
                             t.Type == TransactionType.Adjusted, // Using Adjusted for reverse transactions
                        ct);

                if (existingReverse != null)
                {
                    // Already reversed, return current balance
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

                // Calculate the delta from the original transaction
                // We need to find the transaction before this one to calculate the delta
                var previousTransaction = await _dbContext.Set<Transaction>()
                    .Where(t => t.LoyaltyCardId == originalTransaction.LoyaltyCardId &&
                               t.Id < originalTransaction.Id)
                    .OrderByDescending(t => t.Id)
                    .FirstOrDefaultAsync(ct);

                decimal originalDeltaDecimal;
                if (previousTransaction != null)
                {
                    // Delta = BalanceAfter - PreviousBalanceAfter
                    originalDeltaDecimal = originalTransaction.BalanceAfter - previousTransaction.BalanceAfter;
                }
                else
                {
                    // This is the first transaction, so delta = BalanceAfter
                    originalDeltaDecimal = originalTransaction.BalanceAfter;
                }
                decimal originalDelta = originalDeltaDecimal;

                // Try to parse delta from description if calculation seems wrong
                if (originalDelta == 0m && !string.IsNullOrEmpty(originalTransaction.Description))
                {
                    // Parse from description like "+5" or "-10.5"
                    var desc = originalTransaction.Description;
                    var plusIndex = desc.IndexOf('+');
                    var minusIndex = desc.IndexOf('-');
                    if (plusIndex >= 0)
                    {
                        var numStr = desc.Substring(plusIndex + 1).Split(' ', '\n', '\r')[0];
                        if (decimal.TryParse(numStr, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out decimal parsed))
                            originalDelta = parsed;
                    }
                    else if (minusIndex >= 0 && desc.IndexOf("xərcləndi") < 0) // Not the redeem description
                    {
                        var numStr = desc.Substring(minusIndex + 1).Split(' ', '\n', '\r')[0];
                        if (decimal.TryParse(numStr, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out decimal parsed))
                            originalDelta = -parsed;
                    }
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

                // Update card and save
                cardRepo.Update(card);
                var txRepo = _unitOfWork.Repository<Transaction>();
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
    }
}

