using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Production-da e-kassa SPA WebView capture JSON-larını audit edir ki, real shape
    /// görünsün və `EkassaCaptureResult.fromJsonString` key heuristic-ləri dəqiqləşdirilsin.
    /// Schema sabitləşdikdən sonra (~10-20 sample-dan sonra) bu cədvəl silinə bilər.
    /// </summary>
    public class EkassaJsonAudit : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public int? BranchId { get; set; }
        public int? TransactionId { get; set; }
        public string? QrDocId { get; set; }
        public string RawJson { get; set; } = string.Empty;
        public decimal? ParsedAmount { get; set; }
        public string? ParsedObjectCode { get; set; }
        public string? ParsedDocumentNumber { get; set; }
        public DateTime? ParsedCreatedAtUtc { get; set; }
    }
}
