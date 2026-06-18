using System.Text.Json.Serialization;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.WebAPI.Extensions;
using Loyalty.WebAPI.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    [ApiController]
    [Route("api/wallet")]
    [WalletWebhookAuth]
    public class WalletWebhookController : ControllerBase
    {
        private readonly IWalletWebhookService _webhookService;

        public WalletWebhookController(IWalletWebhookService webhookService)
        {
            _webhookService = webhookService;
        }

        [HttpPost("get_wallet")]
        public async Task<IActionResult> GetWallet([FromBody] GetWalletRequest request, CancellationToken ct)
        {
            var ctx = HttpContext.GetWalletWebhookContext();
            if (ctx == null)
                return Unauthorized();

            var result = await _webhookService.GetWalletAsync(ctx, request.Barcode ?? "", ct);
            if (!result.Success)
                return Ok(new { success = false, message = result.Message });

            return Ok(new
            {
                success = true,
                loyalty = result.Loyalty.Select(l => new
                {
                    id = l.Id,
                    count = l.Count,
                    maximum = l.Maximum,
                    rewards = l.Rewards,
                    product_id = l.ProductId,
                    product_name = l.ProductName,
                    products = l.Products.Select(p => new
                    {
                        product_id = p.ProductId,
                        product_name = p.ProductName
                    })
                }),
                cashback_balans = result.CashbackBalans
            });
        }

        [HttpPost("set_redeem")]
        public async Task<IActionResult> SetRedeem([FromBody] SetRedeemWebhookRequest request, CancellationToken ct)
        {
            var ctx = HttpContext.GetWalletWebhookContext();
            if (ctx == null)
                return Unauthorized();

            var result = await _webhookService.SetRedeemAsync(ctx, new SetRedeemRequest
            {
                VenueId = request.VenueId ?? "",
                Barcode = request.Barcode ?? "",
                CampaignId = request.CampaignId ?? "",
                Type = request.Type ?? "loyalty",
                Amount = request.Amount ?? 0
            }, ct);

            return Ok(new { status = result.Status ? "success" : "error", message = result.Message });
        }

        [HttpPost("set_verify")]
        public async Task<IActionResult> SetVerify([FromBody] SetVerifyWebhookRequest request, CancellationToken ct)
        {
            var ctx = HttpContext.GetWalletWebhookContext();
            if (ctx == null)
                return Unauthorized();

            var result = await _webhookService.SetVerifyAsync(ctx, new SetVerifyRequest
            {
                VenueId = request.VenueId ?? "",
                Barcode = request.Barcode ?? "",
                CampaignId = request.CampaignId ?? "",
                Otp = request.Otp ?? "",
                Type = request.Type ?? "loyalty",
                Amount = request.Amount ?? 0,
                OrderId = request.OrderId
            }, ct);

            if (!result.Status)
                return Ok(new { status = "error" });

            if (result.Type == "cashback")
                return Ok(new { status = "success", type = "cashback", discount = result.Discount });
            return Ok(new { status = "success", type = "loyalty", product_id = result.ProductId, product_name = result.ProductName });
        }

        [HttpPost("iiko_transaction")]
        public async Task<IActionResult> IikoTransaction([FromBody] IikoOrderEventWebhookRequest request, CancellationToken ct)
        {
            var ctx = HttpContext.GetWalletWebhookContext();
            if (ctx == null)
                return Unauthorized();

            var firstCustomer = request.Customer?.FirstOrDefault();

            var internalReq = new IikoOrderEventRequest
            {
                EventType = string.IsNullOrWhiteSpace(request.EventType) ? "OrderClosed" : request.EventType!,
                OrderId = request.OrderId ?? string.Empty,
                OrderNumber = request.OrderNumber,
                TotalSum = request.TotalSum ?? 0m,
                FullSum = request.FullSum ?? 0m,
                DepartmentId = request.Restaurant?.DepartmentId,
                TerminalGroupId = request.TerminalsGroup?.Id,
                ExternalCustomerId = firstCustomer?.Id,
                CustomerPhone = firstCustomer?.Phone,
                CustomerCardNumber = firstCustomer?.CardNumber,
                Items = (request.Items ?? new()).Select(i => new IikoOrderItem
                {
                    ProductId = i.ProductId ?? string.Empty,
                    ProductName = i.ProductName,
                    ProductNumber = i.ProductNumber,
                    Price = i.Price,
                    Amount = i.Amount,
                    Sum = i.Sum
                }).ToList(),
                Discounts = (request.Discounts ?? new()).Select(d => new IikoOrderDiscount
                {
                    DiscountTypeId = d.DiscountTypeId,
                    DiscountTypeName = d.DiscountTypeName,
                    DiscountSum = d.DiscountSum
                }).ToList(),
                Payments = (request.Payments ?? new()).Select(p => new IikoOrderPayment
                {
                    PaymentTypeId = p.PaymentTypeId,
                    PaymentTypeName = p.PaymentTypeName,
                    Sum = p.Sum,
                    Credential = p.AdditionalData?.Credential,
                    SearchScope = p.AdditionalData?.SearchScope
                }).ToList()
            };

            var result = await _webhookService.ProcessIikoOrderEventAsync(ctx, internalReq, ct);
            return Ok(new { success = result.Success, message = result.Message });
        }

        [HttpPost("collect")]
        public async Task<IActionResult> Collect([FromBody] CollectWebhookRequest request, CancellationToken ct)
        {
            var ctx = HttpContext.GetWalletWebhookContext();
            if (ctx == null)
                return Unauthorized();

            var items = (request.Items ?? []).Select(i => new CollectItem
            {
                Id = (i?.Id ?? 0).ToString("D"),
                Name = i?.Name ?? "",
                Count = i?.Count ?? 0
            }).ToList();

            CollectPayments? payments = null;
            if (request.Payments != null)
            {
                payments = new CollectPayments
                {
                    Cash = request.Payments.Cash ?? 0,
                    Cashless = request.Payments.Cashless ?? 0
                };
            }

            var result = await _webhookService.CollectAsync(ctx, new CollectRequest
            {
                Id = request.Id ?? "",
                VenueId = request.VenueId ?? "",
                CustomerId = request.CustomerId ?? "",
                Status = request.Status ?? "closed",
                Total = request.Total ?? 0,
                Items = items,
                Payments = payments
            }, ct);

            return Ok(new { success = result.Success });
        }
    }

    public class GetWalletRequest
    {
        [JsonPropertyName("venue_id")]
        public string? VenueId { get; set; }

        [JsonPropertyName("barcode")]
        public string? Barcode { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    public class SetRedeemWebhookRequest
    {
        [JsonPropertyName("venue_id")]
        public string? VenueId { get; set; }

        [JsonPropertyName("barcode")]
        public string? Barcode { get; set; }

        [JsonPropertyName("campaign_id")]
        public string? CampaignId { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    public class SetVerifyWebhookRequest
    {
        [JsonPropertyName("venue_id")]
        public string? VenueId { get; set; }

        [JsonPropertyName("barcode")]
        public string? Barcode { get; set; }

        [JsonPropertyName("campaign_id")]
        public string? CampaignId { get; set; }

        [JsonPropertyName("otp")]
        public string? Otp { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("order_id")]
        public string? OrderId { get; set; }
    }

    public class CollectWebhookRequest
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("venue_id")]
        public string? VenueId { get; set; }

        [JsonPropertyName("customer_id")]
        public string? CustomerId { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("total")]
        public decimal? Total { get; set; }

        [JsonPropertyName("items")]
        public List<CollectItemDto>? Items { get; set; }

        [JsonPropertyName("payments")]
        public CollectPaymentsDto? Payments { get; set; }
    }

    public class CollectPaymentsDto
    {
        [JsonPropertyName("cash")]
        public decimal? Cash { get; set; }

        [JsonPropertyName("cashless")]
        public decimal? Cashless { get; set; }
    }

    public class CollectItemDto
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("count")]
        public int? Count { get; set; }
    }

    public class IikoWebhookRequest
    {
        [JsonPropertyName("sum")]
        public decimal? Sum { get; set; }

        [JsonPropertyName("balance")]
        public decimal? Balance { get; set; }

        [JsonPropertyName("orderId")]
        public string? OrderId { get; set; }

        [JsonPropertyName("orderNumber")]
        public int? OrderNumber { get; set; }

        [JsonPropertyName("isDelivery")]
        public bool? IsDelivery { get; set; }

        [JsonPropertyName("terminalGroupId")]
        public string? TerminalGroupId { get; set; }

        [JsonPropertyName("walletId")]
        public string? WalletId { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("organizationId")]
        public string? OrganizationId { get; set; }

        [JsonPropertyName("uocId")]
        public string? UocId { get; set; }

        [JsonPropertyName("notificationType")]
        public int? NotificationType { get; set; }

        [JsonPropertyName("customerId")]
        public string? CustomerId { get; set; }

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("transactionType")]
        public string? TransactionType { get; set; }

        [JsonPropertyName("transactionId")]
        public string? TransactionId { get; set; }

        [JsonPropertyName("subscriptionPassword")]
        public string? SubscriptionPassword { get; set; }

        [JsonPropertyName("changedOn")]
        public string? ChangedOn { get; set; }
    }

    public class IikoOrderEventWebhookRequest
    {
        [JsonPropertyName("EventType")] public string? EventType { get; set; }
        [JsonPropertyName("Timestamp")] public DateTimeOffset? Timestamp { get; set; }
        [JsonPropertyName("Restaurant")] public IikoRestaurantDto? Restaurant { get; set; }
        [JsonPropertyName("Terminal")] public IikoNamedIdDto? Terminal { get; set; }
        [JsonPropertyName("TerminalsGroup")] public IikoNamedIdDto? TerminalsGroup { get; set; }
        [JsonPropertyName("OrderId")] public string? OrderId { get; set; }
        [JsonPropertyName("OrderNumber")] public int? OrderNumber { get; set; }
        [JsonPropertyName("TotalSum")] public decimal? TotalSum { get; set; }
        [JsonPropertyName("FullSum")] public decimal? FullSum { get; set; }
        [JsonPropertyName("Table")] public IikoTableDto? Table { get; set; }
        [JsonPropertyName("Cashier")] public IikoNamedIdDto? Cashier { get; set; }
        [JsonPropertyName("Items")] public List<IikoItemDto>? Items { get; set; }
        [JsonPropertyName("Discounts")] public List<IikoDiscountDto>? Discounts { get; set; }
        [JsonPropertyName("Customer")] public List<IikoCustomerDto>? Customer { get; set; }
        [JsonPropertyName("Payments")] public List<IikoPaymentDto>? Payments { get; set; }
    }

    public class IikoRestaurantDto
    {
        [JsonPropertyName("Name")] public string? Name { get; set; }
        [JsonPropertyName("DepartmentId")] public string? DepartmentId { get; set; }
    }

    public class IikoNamedIdDto
    {
        [JsonPropertyName("Id")] public string? Id { get; set; }
        [JsonPropertyName("Name")] public string? Name { get; set; }
    }

    public class IikoTableDto
    {
        [JsonPropertyName("Id")] public string? Id { get; set; }
        [JsonPropertyName("Number")] public int? Number { get; set; }
    }

    public class IikoItemDto
    {
        [JsonPropertyName("ProductId")] public string? ProductId { get; set; }
        [JsonPropertyName("ProductName")] public string? ProductName { get; set; }
        [JsonPropertyName("ProductNumber")] public string? ProductNumber { get; set; }
        [JsonPropertyName("CategoryId")] public string? CategoryId { get; set; }
        [JsonPropertyName("CategoryName")] public string? CategoryName { get; set; }
        [JsonPropertyName("SizeId")] public string? SizeId { get; set; }
        [JsonPropertyName("SizeName")] public string? SizeName { get; set; }
        [JsonPropertyName("Price")] public decimal Price { get; set; }
        [JsonPropertyName("Amount")] public decimal Amount { get; set; }
        [JsonPropertyName("Sum")] public decimal Sum { get; set; }
        [JsonPropertyName("Modifiers")] public List<IikoModifierDto>? Modifiers { get; set; }
    }

    public class IikoModifierDto
    {
        [JsonPropertyName("ProductId")] public string? ProductId { get; set; }
        [JsonPropertyName("ProductName")] public string? ProductName { get; set; }
        [JsonPropertyName("ProductNumber")] public string? ProductNumber { get; set; }
        [JsonPropertyName("Price")] public decimal Price { get; set; }
        [JsonPropertyName("Amount")] public decimal Amount { get; set; }
    }

    public class IikoDiscountDto
    {
        [JsonPropertyName("DiscountTypeId")] public string? DiscountTypeId { get; set; }
        [JsonPropertyName("DiscountTypeName")] public string? DiscountTypeName { get; set; }
        [JsonPropertyName("DiscountSum")] public decimal DiscountSum { get; set; }
    }

    public class IikoCustomerDto
    {
        [JsonPropertyName("Id")] public string? Id { get; set; }
        [JsonPropertyName("Name")] public string? Name { get; set; }
        [JsonPropertyName("Surname")] public string? Surname { get; set; }
        [JsonPropertyName("CardNumber")] public string? CardNumber { get; set; }
        [JsonPropertyName("Gender")] public string? Gender { get; set; }
        [JsonPropertyName("Phone")] public string? Phone { get; set; }
        [JsonPropertyName("Email")] public string? Email { get; set; }
    }

    public class IikoPaymentDto
    {
        [JsonPropertyName("PaymentTypeId")] public string? PaymentTypeId { get; set; }
        [JsonPropertyName("PaymentTypeName")] public string? PaymentTypeName { get; set; }
        [JsonPropertyName("Sum")] public decimal Sum { get; set; }
        [JsonPropertyName("AdditionalData")] public IikoPaymentAdditionalDto? AdditionalData { get; set; }
    }

    public class IikoPaymentAdditionalDto
    {
        [JsonPropertyName("Credential")] public string? Credential { get; set; }
        [JsonPropertyName("SearchScope")] public int? SearchScope { get; set; }
    }
}
