using Loyalty.Business.Models.TenantIntegration;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IWalletWebhookService
    {
        Task<GetWalletResponse> GetWalletAsync(WalletWebhookResolvedContext context, string barcode, CancellationToken ct = default);
        Task<SetRedeemResponse> SetRedeemAsync(WalletWebhookResolvedContext context, SetRedeemRequest request, CancellationToken ct = default);
        Task<SetVerifyResponse> SetVerifyAsync(WalletWebhookResolvedContext context, SetVerifyRequest request, CancellationToken ct = default);
        Task<CollectResponse> CollectAsync(WalletWebhookResolvedContext context, CollectRequest request, CancellationToken ct = default);
    }

    public class GetWalletResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<LoyaltyCampaignItem> Loyalty { get; set; } = new();
        public double CashbackBalans { get; set; }
    }

    public class LoyaltyCampaignItem
    {
        public int Id { get; set; }
        public int Count { get; set; }
        public int Maximum { get; set; }
        public int Rewards { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public List<ProductItem> Products { get; set; } = new();
    }

    public class ProductItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string? ProductName { get; set; }
    }

    public class SetRedeemRequest
    {
        public string VenueId { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string CampaignId { get; set; } = string.Empty;
        public string Type { get; set; } = "loyalty";
        public decimal Amount { get; set; }
        public int Delta { get; set; } = -1;
    }

    public class SetRedeemResponse
    {
        public bool Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class SetVerifyRequest
    {
        public string VenueId { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string CampaignId { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        public string Type { get; set; } = "loyalty";
        public decimal Amount { get; set; }
        public string? OrderId { get; set; }
    }

    public class SetVerifyResponse
    {
        public bool Status { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal? Discount { get; set; }
    }

    public class CollectRequest
    {
        public string Id { get; set; } = string.Empty;
        public string VenueId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string Status { get; set; } = "closed";
        public decimal Total { get; set; }
        public List<CollectItem> Items { get; set; } = new();
        public CollectPayments? Payments { get; set; }
    }

    public class CollectPayments
    {
        public decimal Cash { get; set; }
        public decimal Cashless { get; set; }
    }

    public class CollectItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class CollectResponse
    {
        public bool Success { get; set; }
    }
}
