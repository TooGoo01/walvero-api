using Hangfire;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage.Azure;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Hangfire background job — OCR validation + balance apply / flag for review.
    /// </summary>
    public sealed class ReceiptOcrJob
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReceiptOcrJob> _logger;

        public ReceiptOcrJob(IServiceScopeFactory scopeFactory, ILogger<ReceiptOcrJob> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [Queue("ocr")]
        [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task ProcessAsync(
            int transactionId,
            decimal enteredAmountForOcr,
            decimal toleranceAzn,
            bool alertEnabled,
            string? alertPhone,
            string tenantName,
            string? customerName,
            string? operatorId,
            decimal amountCapForAlert,
            CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<LoyaltyWalletDbContext>();
            var storage = sp.GetRequiredService<IAzureStorage>();
            var validator = sp.GetRequiredService<IReceiptValidationService>();
            var alert = sp.GetRequiredService<IWhatsAppAlertService>();
            var programSvc = sp.GetRequiredService<ILoyaltyProgramRepository>();
            var brandingSvc = sp.GetRequiredService<ITenantBrandingRepository>();

            var tx = await db.Transactions
                .Include(t => t.LoyaltyCard).ThenInclude(c => c.Customer)
                .FirstOrDefaultAsync(t => t.Id == transactionId, ct);

            if (tx == null)
            {
                _logger.LogError("ReceiptOcrJob: Transaction {Id} not found", transactionId);
                return;
            }

            if (tx.Status != RedeemStatus.PendingOcr)
            {
                _logger.LogWarning("ReceiptOcrJob: Transaction {Id} already processed Status={Status}",
                    transactionId, tx.Status);
                return;
            }

            // Download receipt from blob via URL → filename
            var fileName = ExtractBlobFileName(tx.ReceiptImageUrl);
            if (string.IsNullOrEmpty(fileName))
            {
                _logger.LogError("ReceiptOcrJob: Tx {Id} has no receipt URL", transactionId);
                tx.Status = RedeemStatus.FlaggedForReview;
                tx.IsSuspicious = true;
                tx.SuspiciousReason = "OcrDownloadFailed";
                await db.SaveChangesAsync(ct);
                return;
            }

            byte[]? bytes = null;
            try
            {
                bytes = await storage.DownloadAsync("receipts", fileName, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReceiptOcrJob: blob download failed Tx={Id} File={F}", transactionId, fileName);
            }

            if (bytes == null || bytes.Length == 0)
            {
                tx.Status = RedeemStatus.FlaggedForReview;
                tx.IsSuspicious = true;
                tx.SuspiciousReason = "OcrDownloadFailed";
                await db.SaveChangesAsync(ct);
                return;
            }

            var ocr = await validator.ValidateBytesAsync(bytes, enteredAmountForOcr, toleranceAzn, tx.ReceiptImageUrl, ct);
            tx.OcrExtractedAmount = ocr.ExtractedAmount;

            if (ocr.IsValid)
            {
                // OCR uyğun — balansa tətbiq et
                try
                {
                    await ApplyToBalanceAsync(db, tx, programSvc, brandingSvc, ct);
                    tx.Status = RedeemStatus.Applied;
                    _logger.LogInformation("ReceiptOcrJob: Tx {Id} OCR OK, balance applied. Extracted={Amt}",
                        transactionId, ocr.ExtractedAmount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ReceiptOcrJob: balance apply failed Tx={Id}", transactionId);
                    tx.Status = RedeemStatus.FlaggedForReview;
                    tx.IsSuspicious = true;
                    tx.SuspiciousReason = "BalanceApplyFailed";
                }
            }
            else
            {
                tx.Status = RedeemStatus.FlaggedForReview;
                tx.IsSuspicious = true;
                tx.SuspiciousReason = ocr.ExtractedAmount.HasValue ? "OcrMismatch" : "OcrUnreadable";
                _logger.LogWarning("ReceiptOcrJob: Tx {Id} flagged. Reason={Reason} Extracted={Amt}",
                    transactionId, tx.SuspiciousReason, ocr.ExtractedAmount);

                if (alertEnabled && !string.IsNullOrWhiteSpace(alertPhone))
                {
                    try
                    {
                        await alert.SendSuspiciousTxAlertAsync(
                            alertPhone!, tenantName, customerName,
                            Math.Abs(enteredAmountForOcr), amountCapForAlert,
                            operatorId, tx.OrderId ?? tx.Id.ToString(), CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "WhatsApp alert send failed Tx={Id}", transactionId);
                    }
                }
            }

            await db.SaveChangesAsync(ct);
        }

        private static async Task ApplyToBalanceAsync(
            LoyaltyWalletDbContext db,
            Transaction tx,
            ILoyaltyProgramRepository programSvc,
            ITenantBrandingRepository brandingSvc,
            CancellationToken ct)
        {
            var card = await db.Set<LoyaltyCard>()
                .FirstOrDefaultAsync(c => c.Id == tx.LoyaltyCardId, ct);
            if (card == null) throw new InvalidOperationException($"LoyaltyCard {tx.LoyaltyCardId} not found");

            var program = await programSvc.GetByIdNameAsync(card.LoyaltyProgramId, ct);
            if (program == null) throw new InvalidOperationException($"Program {card.LoyaltyProgramId} not found");

            var branding = await brandingSvc.GetByTenantIdAsync(card.TenantId, ct);
            string ToAbs(string? p) => p ?? string.Empty;
            var snapshot = CardTemplateSnapshotFactory.From(program, program.CardTemplate, branding, ToAbs);

            // delta-nı transaction-dan bərpa et: Points = Math.Abs(delta), Type → işarə
            decimal delta = tx.Type == TransactionType.Redeemed ? -tx.Points : tx.Points;

            card.LifetimePoints += 1;
            BalanceMath.ApplyDelta(card, snapshot, delta);
            card.EditDate = DateTime.UtcNow;
            tx.BalanceAfter = card.CurrentPoints;
        }

        private static string? ExtractBlobFileName(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    return Uri.UnescapeDataString(uri.Segments.Last());
                }
                // Url DEYİL → ehtimal sırf fileName
                return url;
            }
            catch
            {
                return null;
            }
        }
    }
}
