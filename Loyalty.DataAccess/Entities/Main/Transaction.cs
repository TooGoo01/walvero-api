using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Transaction : EntityBase,IEntity
    {
        public int LoyaltyCardId { get; set; }

        public TransactionType Type { get; set; }
        public RedeemStatus Status { get; set; } = RedeemStatus.Applied;
        public decimal Points { get; set; }
        public decimal? Amount { get; set; } 

        public string? Description { get; set; }
        public string? ReverseReason { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? OrderId { get; set; }

        public decimal BalanceAfter { get; set; }
        // Timezone tracking
        public DateTimeOffset LocalOccurredAt { get; set; } // local + offset
        public string TimeZoneIdUsed { get; set; } = "UTC"; // tenant's timezone

        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        // Receipt OCR + suspicious-tx audit
        public string? ReceiptImageUrl { get; set; }
        public decimal? OcrExtractedAmount { get; set; }
        public bool IsSuspicious { get; set; } = false;
        public string? SuspiciousReason { get; set; } // "AmountExceedsCap" / "OcrMismatch" / "DuplicateReceiptId" / "ObjectCodeMismatch" / "QrMissing" / "ReceiptDateMismatch" / "QrVerificationFailed"

        // QR + physical-receipt audit
        public string? PhysicalReceiptId { get; set; }   // dedupe key (unique per tenant when not null)
        public string? QrDocId { get; set; }             // e-kassa.gov.az doc parameter
        public DateTime? ReceiptDate { get; set; }       // parsed receipt date (UTC)
        public string? ExtractedObjectCode { get; set; } // obyekt kodu parsed from receipt
        public string? ExtractedTaxNumber { get; set; }  // VÖEN parsed from receipt
        public bool QrVerified { get; set; } = false;    // true → e-kassa JSON verified

        public LoyaltyCard LoyaltyCard { get; set; } = null!;
    }
}
