using Loyalty.Business.Models.TenantIntegration;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IWalletWebhookService
    {
        Task<GetWalletResponse> GetWalletAsync(WalletWebhookResolvedContext context, string barcode, CancellationToken ct = default);
        Task<SetRedeemResponse> SetRedeemAsync(WalletWebhookResolvedContext context, SetRedeemRequest request, CancellationToken ct = default);
        Task<SetVerifyResponse> SetVerifyAsync(WalletWebhookResolvedContext context, SetVerifyRequest request, CancellationToken ct = default);
        Task<CollectResponse> CollectAsync(WalletWebhookResolvedContext context, CollectRequest request, CancellationToken ct = default);
        Task<IikoTransactionResponse> ProcessIikoTransactionAsync(WalletWebhookResolvedContext context, IikoTransactionRequest request, CancellationToken ct = default);
        Task<IikoTransactionResponse> ProcessIikoOrderEventAsync(WalletWebhookResolvedContext context, IikoOrderEventRequest request, CancellationToken ct = default);
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
        public decimal Delta { get; set; } = -1;
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

    public class IikoTransactionRequest
    {
        public decimal Sum { get; set; }
        public decimal Balance { get; set; }
        public string? OrderId { get; set; }
        public int? OrderNumber { get; set; }
        public bool? IsDelivery { get; set; }
        public string? TerminalGroupId { get; set; }
        public string? WalletId { get; set; }
        public string? Id { get; set; }
        public string? OrganizationId { get; set; }
        public string? UocId { get; set; }
        public int? NotificationType { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string? SubscriptionPassword { get; set; }
        public string? ChangedOn { get; set; }
    }

    /// <summary>
    /// iiko sifariş emalının uğursuzluq növü — retry siyasətini müəyyən edir.
    /// Transient: DB timeout / deadlock / lock — tez və qısa retry.
    /// Retriable: müştəri/konfiq hələ yoxdur — yavaş retry, 7 günə qədər.
    /// Permanent: yanlış payload / deterministik xəta — retry mənasızdır (Rejected).
    /// </summary>
    public enum IikoFailureKind
    {
        None = 0,
        Transient = 1,
        Retriable = 2,
        Permanent = 3
    }

    public class IikoTransactionResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        /// <summary>Success=false olanda uğursuzluq növü (Success=true → None).</summary>
        public IikoFailureKind FailureKind { get; set; } = IikoFailureKind.None;
    }

    /// <summary>
    /// iiko POS-dan gələn OrderClosed / OrderReturned event-i.
    /// Köhnə sum/balance/transactionId model-i yox, yeni payload formatı.
    /// </summary>
    public class IikoOrderEventRequest
    {
        public string EventType { get; set; } = "OrderClosed";
        public string OrderId { get; set; } = string.Empty;
        public int? OrderNumber { get; set; }
        public decimal TotalSum { get; set; }
        public decimal FullSum { get; set; }
        public string? DepartmentId { get; set; }
        public string? TerminalGroupId { get; set; }
        public string? ExternalCustomerId { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerCardNumber { get; set; }
        public List<IikoOrderItem> Items { get; set; } = new();
        public List<IikoOrderDiscount> Discounts { get; set; } = new();
        public List<IikoOrderPayment> Payments { get; set; } = new();
    }

    public class IikoOrderItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public string? ProductNumber { get; set; }
        /// <summary>iiko məhsul kateqoriyası (plugin göndərirsə) — bonus istisnası üçün fallback.</summary>
        public string? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public decimal Price { get; set; }
        public decimal Amount { get; set; }
        public decimal Sum { get; set; }
    }

    public class IikoOrderDiscount
    {
        public string? DiscountTypeId { get; set; }
        public string? DiscountTypeName { get; set; }
        public decimal DiscountSum { get; set; }
        public List<IikoOrderDiscountByItem> DiscountByItem { get; set; } = new();
    }

    public class IikoOrderDiscountByItem
    {
        public string? OrderItemId { get; set; }
        public string? ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal DiscountSum { get; set; }
    }

    public class IikoOrderPayment
    {
        public string? PaymentTypeId { get; set; }
        public string? PaymentTypeName { get; set; }
        public decimal Sum { get; set; }
        public string? Credential { get; set; }
        public int? SearchScope { get; set; }
    }

}
