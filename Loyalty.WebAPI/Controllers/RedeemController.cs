using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Enums;

using LoyaltyWalletApp.WEBUI.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Policy = "TenantAdminOrModerator")]
    public sealed class RedeemController : ControllerBase
    {
        private readonly ITenantProvider _tenantProvider;
        private readonly IRedeemService _redeemService;
        private readonly ILoyaltyProgramService _programSvc;
        private readonly ILoyaltyCardService _cardSvc;
        private readonly ICustomerEnrollmentService _customerSvc;
        private readonly IPasskitPushService _passkitPushService;
        private readonly ILogger<RedeemController> _logger;

        public RedeemController(
            ITenantProvider tenantProvider,
            IRedeemService redeemService,
            ILoyaltyProgramService programSvc,
            ICustomerEnrollmentService customerSvc,
            ILoyaltyCardService cardSvc,
            ILogger<RedeemController> logger,
            IPasskitPushService passkitPushService)
        {
            _tenantProvider = tenantProvider;
            _redeemService = redeemService;
            _programSvc = programSvc;
            _customerSvc = customerSvc;
            _cardSvc = cardSvc;
            _logger = logger;
            _passkitPushService = passkitPushService;
        }

        // === UI CONFIG ===
        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ProgramUiConfigDto>> UiConfig(CancellationToken ct)
        {
            var programId = _tenantProvider.CurrentProgramId ?? 0;
            _logger.LogDebug("UiConfig called for ProgramId={ProgramId}", programId);

            var programModel = await _programSvc.Get(programId);
            var program = programModel.Data as GetLoyaltyProgramDto;
            if (program is null)
            {
                _logger.LogWarning("UiConfig: program not found for ProgramId={ProgramId}", programId);
                return NotFound(new { success = false, message = "Program not found" });
            }

            var tpl = program.CardTemplate;
            if (tpl is null)
            {
                _logger.LogWarning("UiConfig: CardTemplate is null for ProgramId={ProgramId}", programId);
                return BadRequest(new { success = false, message = "Card template is missing" });
            }

            var dto = new ProgramUiConfigDto
            {
                Success = true,
                ProgramName = program.ProgramName,
                TemplateType = tpl.TemplateType,
                ShowBalanceOnUi = true
            };

            if (tpl.TemplateType == CardTemplateType.TierBased)
            {
                dto.Tiers.Add(new ProgramUiConfigDto.TierRow { Name = "Bronze", Threshold = tpl.SilverThreshold, Percent = tpl.BronzePercent, IconUrl = AbsHttps(tpl.BronzeIconUrl) });
                dto.Tiers.Add(new ProgramUiConfigDto.TierRow { Name = "Silver", Threshold = tpl.GoldThreshold, Percent = tpl.SilverPercent, IconUrl = AbsHttps(tpl.SilverIconUrl) });
                dto.Tiers.Add(new ProgramUiConfigDto.TierRow { Name = "Gold", Threshold = tpl.PlatinumThreshold, Percent = tpl.GoldPercent, IconUrl = AbsHttps(tpl.GoldIconUrl) });
                dto.Tiers.Add(new ProgramUiConfigDto.TierRow { Name = "Platinum", Threshold = null, Percent = tpl.PlatinumPercent, IconUrl = AbsHttps(tpl.PlatinumIconUrl) });
            }

            return Ok(dto);
        }
        public static (string tierName, int? percent,int? percentCash, string? iconUrl) TierResolver(
  int points, GetCardTemplateDto tpl, Func<string?, string?>? toAbsHttps = null)
        {
            string? H(string? url) =>
                toAbsHttps is null ? EnsureHttpsOrNull(url) : EnsureHttpsOrNull(toAbsHttps(url));

            // Threshold-lar (Bronze üçün default = 0)
            int bronzeTh = tpl.BronzeThreshold ?? 0;
            int? silverTh = tpl.SilverThreshold;
            int? goldTh = tpl.GoldThreshold;
            int? platTh = tpl.PlatinumThreshold;

            // Ardıcıllıq pozularsa, artan qaydada düzəlt (opsional, amma təhlükəsizdir)
            if (silverTh.HasValue && silverTh.Value < bronzeTh) silverTh = bronzeTh;
            if (goldTh.HasValue && silverTh.HasValue && goldTh.Value < silverTh.Value) goldTh = silverTh;
            if (platTh.HasValue && goldTh.HasValue && platTh.Value < goldTh.Value) platTh = goldTh;

            // Tier seçimi (>= minimal reqem)
            string name = "Bronze";
            int? percent = tpl.BronzePercent;
            int? percentCash = tpl.BronzePercentCash;
            string? icon = FirstNonEmpty(H(tpl.BronzeIconUrl), H(tpl.LogoUrl));

            if (platTh.HasValue && points >= platTh.Value)
            {
                name = "Platinum";
                percent = tpl.PlatinumPercent;
                percentCash = tpl.PlatinumPercentCash;
                icon = FirstNonEmpty(H(tpl.PlatinumIconUrl), icon);
            }
            else if (goldTh.HasValue && points >= goldTh.Value)
            {
                name = "Gold";
                percent = tpl.GoldPercent;
                percentCash = tpl.GoldPercentCash;
                icon = FirstNonEmpty(H(tpl.GoldIconUrl), icon);
            }
            else if (silverTh.HasValue && points >= silverTh.Value)
            {
                name = "Silver";
                percent = tpl.SilverPercent;
                percentCash = tpl.SilverPercentCash;
                icon = FirstNonEmpty(H(tpl.SilverIconUrl), icon);
            }
            else
            {
                // points < SilverThreshold → Bronze qalır (hətta < BronzeThreshold olsa belə)
                name = "Bronze";
                percent = tpl.BronzePercent;
                percentCash = tpl.BronzePercentCash;
                icon = FirstNonEmpty(H(tpl.BronzeIconUrl), H(tpl.LogoUrl));
            }

            return (name, percent,percentCash, icon);
        }

        public static (string tierName, int? percent, int? percentCash, string? iconUrl) TierResolverDouble(
double points, GetCardTemplateDto tpl, Func<string?, string?>? toAbsHttps = null)
        {
            string? H(string? url) =>
                toAbsHttps is null ? EnsureHttpsOrNull(url) : EnsureHttpsOrNull(toAbsHttps(url));

            // Threshold-lar (Bronze üçün default = 0)
            int bronzeTh = tpl.BronzeThreshold ?? 0;
            int? silverTh = tpl.SilverThreshold;
            int? goldTh = tpl.GoldThreshold;
            int? platTh = tpl.PlatinumThreshold;

            // Ardıcıllıq pozularsa, artan qaydada düzəlt (opsional, amma təhlükəsizdir)
            //if (silverTh.HasValue && silverTh.Value < bronzeTh) silverTh = bronzeTh;
            //if (goldTh.HasValue && silverTh.HasValue && goldTh.Value < silverTh.Value) goldTh = silverTh;
            //if (platTh.HasValue && goldTh.HasValue && platTh.Value < goldTh.Value) platTh = goldTh;

            // Tier seçimi (>= minimal reqem)
            string name = "Bronze";
            int? percent = tpl.BronzePercent;
            int? percentCash = tpl.BronzePercentCash;
            string? icon = FirstNonEmpty(H(tpl.BronzeIconUrl), H(tpl.LogoUrl));

            if (platTh.HasValue && points >= platTh.Value)
            {
                name = "Platinum";
                percent = tpl.PlatinumPercent;
                percentCash = tpl.PlatinumPercentCash;
                icon = FirstNonEmpty(H(tpl.PlatinumIconUrl), icon);
            }
            else if (goldTh.HasValue && points >= goldTh.Value)
            {
                name = "Gold";
                percent = tpl.GoldPercent;
                percentCash = tpl.GoldPercentCash;
                icon = FirstNonEmpty(H(tpl.GoldIconUrl), icon);
            }
            else if (silverTh.HasValue && points >= silverTh.Value)
            {
                name = "Silver";
                percent = tpl.SilverPercent;
                percentCash = tpl.SilverPercentCash;
                icon = FirstNonEmpty(H(tpl.SilverIconUrl), icon);
            }
            else
            {
                // points < SilverThreshold → Bronze qalır (hətta < BronzeThreshold olsa belə)
                name = "Bronze";
                percent = tpl.BronzePercent;
                percentCash = tpl.BronzePercentCash;
                icon = FirstNonEmpty(H(tpl.BronzeIconUrl), H(tpl.LogoUrl));
            }

            return (name, percent,percentCash, icon);
        }
        // === LOOKUP BY CODE ===
        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> LookupByCode(
           
            string code,
            double currentPoints = 0,
            CancellationToken ct = default)
        {
            _logger.LogInformation("LookupByCode called for ProgramId={ProgramId}, Code={Code}", code);

            var tenantId = _tenantProvider.CurrentTenantId ?? 0;
            var programId = _tenantProvider.CurrentProgramId ?? 0;
            var cardModel = await _cardSvc.GetByBarcodeValueAsync(tenantId, (code ?? "").Trim());
            var card = cardModel.Data as GetLoyaltyCardDto;
            if (card is null)
            {
                _logger.LogWarning("Card not found for Code={Code}", code);
                return NotFound(new { success = false, message = "Card not found" });
            }

            var customerModel = await _customerSvc.GetByIdName(card.CustomerId, "N/A");
            var customer = customerModel.Data as GetCustomerDto;

            var programModel = await _programSvc.Get(programId);
            var program = programModel.Data as GetLoyaltyProgramDto;
            var tpl = program.CardTemplate;

            string? tierName = null; int? tierPercent = null; int? tierPercentCash = null; string? tierIconUrl = null;
            if (tpl.TemplateType == CardTemplateType.TierBased)
            {
                if (tpl.OneRedeem == true)
                    (tierName, tierPercent,tierPercentCash, tierIconUrl) = TierResolverDouble(currentPoints, tpl, AbsHttps);
                else
                    (tierName, tierPercent,tierPercentCash, tierIconUrl) = TierResolver(card.CurrentPoints, tpl, AbsHttps);
            }

            return Ok(new
            {
                success = true,
                customerFullName = (customer?.FirstName + " " + customer?.LastName)?.Trim(),
                cardNumber = card.CardNumber,
                currentPoints = card.CurrentPoints,
                tierName,
                tierPercent,
                tierPercentCash,
                tierIconUrl
            });
        }

        // === START REDEEM ===
        public sealed record StartRedeemRequest( string Code, int Delta, string OrderId,string mode,string paymentMethod);

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Start([FromBody] StartRedeemRequest model, CancellationToken ct)
        {
            _logger.LogInformation("Start redeem called: ProgramId={ProgramId}, Code={Code}, Delta={Delta}",
               model.Code, model.Delta);

            var tenantId = _tenantProvider.CurrentTenantId;
            var programId = _tenantProvider.CurrentProgramId;
            if (tenantId is null)
            {
                _logger.LogWarning("Redeem Start failed: TenantId is null");
                return BadRequest(new { success = false, message = "Tenant subdomendən təyin olunmadı." });
            }
            var cardModel = await _cardSvc.GetByBarcodeValueAsync(tenantId.Value, (model.Code ?? "").Trim());
            var card = cardModel.Data as GetLoyaltyCardDto;
           

            var customerModel = await _customerSvc.GetByIdName(card.CustomerId, "N/A");
            var customer = customerModel.Data as GetCustomerDto;

            var programModel = await _programSvc.Get(programId.Value);
            var program = programModel.Data as GetLoyaltyProgramDto;
            var tpl = program.CardTemplate;

            string? tierName = null; int? tierPercent = null; int? tierPercentCash = null; string? tierIconUrl = null;
            if (tpl.TemplateType == CardTemplateType.TierBased)
            {


                if (tpl.TemplateType == CardTemplateType.TierBased)
                {
                    if (tpl.OneRedeem == true)
                        (tierName, tierPercent,tierPercentCash, tierIconUrl) = TierResolverDouble(model.Delta, tpl, AbsHttps);
                    else
                        (tierName, tierPercent, tierPercentCash, tierIconUrl) = TierResolver(card.CurrentPoints, tpl, AbsHttps);
                }


            }
           
            int delta = model.Delta ;
            int? percent=model.paymentMethod=="cash" ? tierPercentCash : tierPercent;
            if (delta > 0) {
                delta = delta * percent.Value / 100;
            }
          


            var dto = new RedeemRequestDto
            {
                LoyaltyProgramId = programId,
                Code = (model.Code ?? "").Trim(),
                Delta = delta,
                OrderId = model.OrderId,
                TenantId = tenantId.Value
            };

            var res = await _redeemService.RedeemAsync(dto, ct);
            return Ok(res);
        }

        // === CONFIRM REDEEM ===
        public sealed record ConfirmRedeemRequest(int RequestId, string Code);

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Confirm([FromBody] ConfirmRedeemRequest model, CancellationToken ct)
        {
            _logger.LogInformation("ConfirmRedeem called for RequestId={RequestId}", model.RequestId);

            var res = await _redeemService.ConfirmRedeemAsync(model.RequestId, (model.Code ?? "").Trim(), ct);
            return Ok(res);
        }

        // helper-lər eyni qala bilər:
        private string? AbsHttps(string? urlOrPath)
        {
            // eyni implementasiya, amma API controllerdə Request.Host-dan istifadə edirsənsə,
            // burası da işləyəcək
            if (string.IsNullOrWhiteSpace(urlOrPath)) return null;
            if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var u))
            {
                if (u.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                    return urlOrPath;
                var https = new UriBuilder(u) { Scheme = "https", Port = -1 }.Uri.ToString();
                return https;
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            if (!baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                baseUrl = "https://" + Request.Host;

            var rel = "/" + urlOrPath.TrimStart('/', '\\');
            return baseUrl + rel;
        }
        private static string? EnsureHttpsOrNull(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;    
            if (Uri.TryCreate(url, UriKind.Absolute, out var u) &&
                u.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                return url;
            return null;
        }

        private static string? FirstNonEmpty(params string?[] urls)
        {
            foreach (var u in urls)
                if (!string.IsNullOrWhiteSpace(u)) return u;
            return null;
        }
        // TierResolver, TierResolverDouble, EnsureHttpsOrNull, FirstNonEmpty eyni qalır
        // (sənin controllerdə necə idisə, copy-paste)
    }
}
