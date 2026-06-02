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
    /// Hangfire background job — QR/OCR validation + dedupe + balance apply / flag for review.
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
            int tenantId,
            int? branchId,
            string ocrLabelAmount,
            string ocrLabelObjectCode,
            string ocrLabelReceiptId,
            string ocrLabelTaxNumber,
            string timeZoneId,
            string? branchObjectCode,
            string? expectedTaxNumber,
            bool requireQrVerification,
            bool notifyOnMissingQr,
            string? qrDocId,
            string ekassaUrlTemplate,
            CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<LoyaltyWalletDbContext>();
            var storage = sp.GetRequiredService<IAzureStorage>();
            var validator = sp.GetRequiredService<IReceiptValidationService>();
            var alert = sp.GetRequiredService<IWhatsAppAlertService>();
            var userNotify = sp.GetRequiredService<IUserNotificationService>();
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

            var fileName = ExtractBlobFileName(tx.ReceiptImageUrl);
            if (string.IsNullOrEmpty(fileName))
            {
                _logger.LogError("ReceiptOcrJob: Tx {Id} has no receipt URL", transactionId);
                FlagTx(tx, "OcrDownloadFailed");
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
                FlagTx(tx, "OcrDownloadFailed");
                await db.SaveChangesAsync(ct);
                return;
            }

            var ctx = new ReceiptValidationContext
            {
                EnteredAmount = enteredAmountForOcr,
                ToleranceAzn = toleranceAzn,
                TenantId = tenantId,
                BranchId = branchId,
                OcrLabelAmount = ocrLabelAmount,
                OcrLabelObjectCode = ocrLabelObjectCode,
                OcrLabelReceiptId = ocrLabelReceiptId,
                OcrLabelTaxNumber = ocrLabelTaxNumber,
                TimeZoneId = timeZoneId,
                BranchObjectCode = branchObjectCode,
                ExpectedTaxNumber = expectedTaxNumber,
                RequireQrVerification = requireQrVerification,
                QrDocIdFromScanner = qrDocId,
                EkassaUrlTemplate = ekassaUrlTemplate,
            };

            var ocr = await validator.ValidateBytesAsync(bytes, ctx, tx.ReceiptImageUrl, ct);

            // Copy audit fields regardless of valid/invalid
            tx.OcrExtractedAmount = ocr.ExtractedAmount;
            tx.QrDocId = ocr.QrDocId;
            tx.PhysicalReceiptId = ocr.PhysicalReceiptId;
            tx.ReceiptDate = ocr.ReceiptDate;
            tx.ExtractedObjectCode = ocr.ExtractedObjectCode;
            tx.ExtractedTaxNumber = ocr.ExtractedTaxNumber;
            tx.QrVerified = ocr.QrVerified;

            // E-kassa fetch audit (qrDocId varsa, fetch nəticələrini saxla)
            if (!string.IsNullOrWhiteSpace(qrDocId))
            {
                try
                {
                    db.Set<EkassaJsonAudit>().Add(new EkassaJsonAudit
                    {
                        TenantId = tenantId,
                        BranchId = branchId,
                        TransactionId = transactionId,
                        QrDocId = qrDocId,
                        RawJson = $"{{\"fetched\":{(ocr.QrVerified ? "true" : "false")},\"url\":\"{ekassaUrlTemplate.Replace("{docId}", qrDocId)}\"}}",
                        ParsedAmount = ocr.ExtractedAmount,
                        ParsedObjectCode = ocr.ExtractedObjectCode,
                        ParsedDocumentNumber = ocr.PhysicalReceiptId,
                        ParsedCreatedAtUtc = ocr.ReceiptDate,
                        RegDate = DateTime.UtcNow,
                    });
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "EkassaJsonAudit save failed Tx={Id}", transactionId);
                }
            }

            // Priority 1: dedupe (overrides whatever the validator reported)
            if (!string.IsNullOrWhiteSpace(ocr.PhysicalReceiptId))
            {
                var existsAlready = await db.Transactions
                    .AsNoTracking()
                    .Where(t => t.Id != tx.Id && t.PhysicalReceiptId == ocr.PhysicalReceiptId)
                    .Join(db.Set<LoyaltyCard>().AsNoTracking(),
                          t => t.LoyaltyCardId,
                          c => c.Id,
                          (t, c) => new { t, c })
                    .AnyAsync(x => x.c.TenantId == tenantId, ct);
                if (existsAlready)
                {
                    FlagTx(tx, "DuplicateReceiptId");
                    await db.SaveChangesAsync(ct);
                    // WhatsApp alert admin-ə (customer FCM göndərilmir — bu fraud cəhdidir,
                    // admin xəbərdar olmalıdır, customer-a deyil).
                    if (!string.IsNullOrWhiteSpace(alertPhone))
                    {
                        await SendWhatsAppAlertAsync(db, alert, tx, tenantName, alertPhone!, operatorId,
                            customerName, enteredAmountForOcr, ocr.ExtractedAmount, amountCapForAlert, "DuplicateReceiptId");
                    }
                    return;
                }
            }

            if (ocr.IsValid)
            {
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
                    FlagTx(tx, "BalanceApplyFailed");
                }
            }
            else
            {
                var reason = ocr.FailReasonCode ?? (ocr.ExtractedAmount.HasValue ? "OcrMismatch" : "OcrUnreadable");
                FlagTx(tx, reason);

                _logger.LogWarning("ReceiptOcrJob: Tx {Id} flagged. Reason={Reason} Extracted={Amt}",
                    transactionId, reason, ocr.ExtractedAmount);

                // WhatsApp alert admin-ə — bütün suspicious reason-larda göndərilir
                // (alertPhone konfiqurasiya olmuşsa). EnableSuspiciousAlert flag-i
                // artıq tələb olunmur — fraud-tipli flagged tx hər zaman admin diqqətini tələb edir.
                // QrMissing halında notifyOnMissingQr toggle WhatsApp-i təsir edir (opt-in).
                bool sendWhatsApp = !string.IsNullOrWhiteSpace(alertPhone)
                    && (reason != "QrMissing" || notifyOnMissingQr);
                if (sendWhatsApp)
                {
                    await SendWhatsAppAlertAsync(db, alert, tx, tenantName, alertPhone!, operatorId,
                        customerName, enteredAmountForOcr, ocr.ExtractedAmount, amountCapForAlert, reason);
                }

                // Customer FCM bütün fraud/error halları üçün söndürülüb. Customer yalnız
                // çekin qəbul edildiyi anda "yoxlanışdan sonra balans artacaq" bildirişi alır
                // (RedeemService.cs-də enqueue-dan sonra). Admin WhatsApp ilə xəbərlənir.
            }

            await db.SaveChangesAsync(ct);
        }

        private static void FlagTx(Transaction tx, string reason)
        {
            tx.Status = RedeemStatus.FlaggedForReview;
            tx.IsSuspicious = true;
            tx.SuspiciousReason = reason;
        }

        private async Task SendWhatsAppAlertAsync(
            LoyaltyWalletDbContext db,
            IWhatsAppAlertService alert,
            Transaction tx,
            string tenantName,
            string alertPhone,
            string? operatorId,
            string? customerName,
            decimal enteredAmountForOcr,
            decimal? ocrAmount,
            decimal amountCapForAlert,
            string reason)
        {
            try
            {
                string? branchName = tx.BranchId.HasValue
                    ? await db.Branches.AsNoTracking()
                        .Where(b => b.Id == tx.BranchId.Value)
                        .Select(b => (string?)b.Name)
                        .FirstOrDefaultAsync(CancellationToken.None)
                    : null;

                string? operatorName = null;
                if (!string.IsNullOrWhiteSpace(operatorId))
                {
                    int.TryParse(operatorId, out var intId);
                    var u = await db.Users.AsNoTracking()
                        .Where(x => (intId > 0 && x.Id == intId) || x.UserName == operatorId || x.PhoneNumber == operatorId)
                        .Select(x => new { x.Name, x.Surname, x.UserName, x.PhoneNumber })
                        .FirstOrDefaultAsync(CancellationToken.None);
                    if (u != null)
                    {
                        var full = $"{u.Name} {u.Surname}".Trim();
                        operatorName = !string.IsNullOrWhiteSpace(full) ? full : (u.UserName ?? u.PhoneNumber ?? operatorId);
                    }
                    else operatorName = operatorId;
                }

                string? betterCustomerName = customerName;
                if (tx.LoyaltyCard?.Customer != null)
                {
                    var c = tx.LoyaltyCard.Customer;
                    var full = $"{c.FirstName} {c.LastName}".Trim();
                    if (!string.IsNullOrWhiteSpace(full)) betterCustomerName = full;
                }

                await alert.SendSuspiciousTxAlertAsync(
                    alertPhone, tenantName, branchName, betterCustomerName, operatorName,
                    Math.Abs(enteredAmountForOcr), ocrAmount,
                    amountCapForAlert > 0 ? amountCapForAlert : (decimal?)null,
                    reason,
                    tx.OrderId ?? tx.Id.ToString(), CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WhatsApp alert send failed Tx={Id}", tx.Id);
            }
        }

        private async Task NotifyCustomerAsync(IUserNotificationService notify, Transaction tx, string tenantName, string reason, bool isDuplicate)
        {
            try
            {
                var customerId = tx.LoyaltyCard?.Customer?.Id;
                if (customerId.HasValue)
                {
                    await notify.NotifyReceiptIssueAsync(customerId.Value, tenantName, reason, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User FCM notify failed Tx={Id} Reason={R}", tx.Id, reason);
            }
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
                return url;
            }
            catch
            {
                return null;
            }
        }
    }
}
