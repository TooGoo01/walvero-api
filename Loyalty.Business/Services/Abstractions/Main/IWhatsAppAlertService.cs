namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Suspicious tx alert üçün ətraflı kontekst — bütün extracted/expected dəyərlər
    /// admin WhatsApp mesajında göstərilir. Opsional sahələr null ola bilər.
    /// </summary>
    public sealed class SuspiciousAlertContext
    {
        public required string ToPhone { get; init; }
        public required string TenantName { get; init; }
        public string? BranchName { get; init; }
        public string? CustomerName { get; init; }
        public string? OperatorName { get; init; }
        public decimal EnteredAmount { get; init; }
        public decimal? OcrAmount { get; init; }
        public decimal? Cap { get; init; }
        public string? SuspiciousReason { get; init; }
        public required string TxReference { get; init; }

        // Extended context (Phase 2 — all reasons)
        public string? PhysicalReceiptId { get; init; }
        public string? QrDocId { get; init; }
        public DateTime? ReceiptDate { get; init; }
        public string? ExtractedObjectCode { get; init; }
        public string? ExpectedObjectCode { get; init; }
        public string? ExtractedTaxNumber { get; init; }
        public string? ExpectedTaxNumber { get; init; }
        public string? TimeZoneId { get; init; }
    }

    public interface IWhatsAppAlertService
    {
        /// <summary>
        /// Suspicious-tx hadisəsi haqqında tenant admin-inin WhatsApp nömrəsinə xəbərdarlıq mesajı göndərir.
        /// Send failure caller-ı bloklamır — implementation öz daxilində log edib silently fail edir.
        /// </summary>
        Task SendSuspiciousTxAlertAsync(
            string toPhone,
            string tenantName,
            string? branchName,
            string? customerName,
            string? operatorName,
            decimal enteredAmount,
            decimal? ocrAmount,
            decimal? cap,
            string? suspiciousReason,
            string txReference,
            CancellationToken ct = default);

        /// <summary>
        /// Yeni overload — bütün kontekst dəyərləri ilə (extractedObjectCode, taxNumber, date və s.).
        /// </summary>
        Task SendSuspiciousTxAlertAsync(SuspiciousAlertContext context, CancellationToken ct = default);
    }
}
