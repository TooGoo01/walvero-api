namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IMonyoPosApiClient
    {
        Task<MonyoCustomerResponse[]?> AddCustomersAsync(string baseUrl, string apiToken, string venueId, MonyoCustomerDto[] customers, CancellationToken ct = default);
        Task<MonyoCustomerResponse[]?> UpdateCustomersAsync(string baseUrl, string apiToken, string venueId, MonyoCustomerUpdateDto[] customers, CancellationToken ct = default);
    }

    public class MonyoCustomerDto
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Birthday { get; set; }
        public string? CustomerCode { get; set; }
        public string? Barcode { get; set; }
        public string? Discount { get; set; }
        public string? Balans { get; set; }
        public string? Note { get; set; }
    }

    public class MonyoCustomerUpdateDto : MonyoCustomerDto
    {
        public string MonyoId { get; set; } = string.Empty;
    }

    public class MonyoCustomerResponse
    {
        public int Id { get; set; }
        public string? CustomerCode { get; set; }
        public string? Barcode { get; set; }
        public long? WalveroId { get; set; }
    }
}
