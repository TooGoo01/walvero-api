namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>Çek şəklindən struktur sahələri Azure OpenAI vision ilə oxuyur — yeganə OCR mənbəyi.</summary>
    public interface IReceiptVisionOcr
    {
        Task<VisionReceiptFields> ReadAsync(byte[] imageBytes, VisionReceiptHints? hints = null, CancellationToken ct = default);
    }

    /// <summary>
    /// Tenant-konfiqurasiyalı label siyahıları (vergüllə) — modelə ipucu kimi gedir.
    /// Gözlənilən dəyərlər (VÖEN, obyekt kodu, filial adı) qəsdən göndərilmir: model "uyğunlaşdırmasın",
    /// müqayisə C#-da aparılır.
    /// </summary>
    public sealed record VisionReceiptHints(
        string? AmountLabels = null,
        string? ObjectCodeLabels = null,
        string? ReceiptIdLabels = null,
        string? TaxNumberLabels = null);

    public sealed class VisionReceiptFields
    {
        public bool Ok { get; set; }
        /// <summary>429/5xx/timeout — xidmət müvəqqəti əlçatmazdır (konfiqurasiya və ya parse xətası deyil).</summary>
        public bool Transient { get; set; }
        public decimal? Amount { get; set; }
        public string? ObjectCode { get; set; }
        public string? TaxNumber { get; set; }
        public string? ReceiptId { get; set; }
        public DateTime? Date { get; set; }
        public string? OrderNo { get; set; }
        public bool IsFiscal { get; set; }
        /// <summary>Başlıqda çap olunan müəssisə / filial adı.</summary>
        public string? MerchantName { get; set; }
        /// <summary>Çekin sətir-sətir tam transkripsiyası (regex fallback + filial adı axtarışı üçün).</summary>
        public string? RawText { get; set; }
        /// <summary>Modelin qaytardığı xam JSON (log/diaqnostika).</summary>
        public string? RawJson { get; set; }
    }
}
