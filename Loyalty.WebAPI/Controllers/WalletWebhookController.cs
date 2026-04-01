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
}
