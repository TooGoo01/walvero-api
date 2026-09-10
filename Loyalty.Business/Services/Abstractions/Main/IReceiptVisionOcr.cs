namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>Çek şəklindən struktur sahələri Azure OpenAI vision ilə oxuyur.</summary>
    public interface IReceiptVisionOcr
    {
        Task<VisionReceiptFields> ReadAsync(byte[] imageBytes, CancellationToken ct = default);
    }

    public sealed class VisionReceiptFields
    {
        public bool Ok { get; set; }
        public decimal? Amount { get; set; }
        public string? ObjectCode { get; set; }
        public string? TaxNumber { get; set; }
        public string? ReceiptId { get; set; }
        public DateTime? Date { get; set; }
        public string? OrderNo { get; set; }
        public bool IsFiscal { get; set; }
    }
}
