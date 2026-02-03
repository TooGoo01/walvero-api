using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using LoyaltyWalletApp.WEBUI.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly ICustomerRepository _customerRepository;
        private readonly ILogger<RedeemController> _logger;

        public RedeemController(
            ITenantProvider tenantProvider,
            IRedeemService redeemService,
            ILoyaltyProgramService programSvc,
            ICustomerEnrollmentService customerSvc,
            ILoyaltyCardService cardSvc,
            ILogger<RedeemController> logger,
            IPasskitPushService passkitPushService,
            ICustomerRepository customerRepository)
        {
            _tenantProvider = tenantProvider;
            _redeemService = redeemService;
            _programSvc = programSvc;
            _customerSvc = customerSvc;
            _cardSvc = cardSvc;
            _logger = logger;
            _passkitPushService = passkitPushService;
            _customerRepository = customerRepository;
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
            var codeStr = (code ?? "").Trim();
            
            GetLoyaltyCardDto? card = null;
            GetCustomerDto? customer = null;
            
            // First, try to find by barcode (priority)
            var cardModel = await _cardSvc.GetByBarcodeValueAsync(tenantId, codeStr);
            card = cardModel.Data as GetLoyaltyCardDto;
            
            if (card == null && long.TryParse(codeStr, out long walveroId))
            {
                // Barcode not found, try WalveroId
                _logger.LogInformation("Barcode not found, trying WalveroId: {WalveroId}", walveroId);
                
                // Find customer by WalveroId
                var customerEntity = await _customerRepository.GetByWalveroIdAsync(tenantId, walveroId, ct);
                
                if (customerEntity == null)
                {
                    _logger.LogWarning("Customer not found by WalveroId: {WalveroId} for TenantId: {TenantId}", walveroId, tenantId);
                    return NotFound(new { success = false, message = "Card not found" });
                }

                // Get customer DTO
                var customerModel = await _customerSvc.GetByIdName(customerEntity.Id, "N/A");
                customer = customerModel.Data as GetCustomerDto;

                if (customer == null)
                {
                    _logger.LogWarning("Customer DTO not found for CustomerId: {CustomerId}", customerEntity.Id);
                    return NotFound(new { success = false, message = "Card not found" });
                }

                // Find the active card for this customer and program
                if (programId > 0 && customer.LoyaltyCards != null && customer.LoyaltyCards.Any())
                {
                    card = customer.LoyaltyCards
                        .Where(c => c.LoyaltyProgramId == programId)
                        .OrderByDescending(c => c.Id)
                        .FirstOrDefault();
                }

                if (card == null)
                {
                    _logger.LogWarning("Active card not found for CustomerId: {CustomerId}, ProgramId: {ProgramId}", customer.Id, programId);
                    return NotFound(new { success = false, message = "Card not found" });
                }
            }
            else if (card == null)
            {
                // Neither barcode nor WalveroId found
                _logger.LogWarning("Card not found for Code={Code}", code);
                return NotFound(new { success = false, message = "Card not found" });
            }
            else
            {
                // Card found by barcode, get customer
                var customerModel = await _customerSvc.GetByIdName(card.CustomerId, "N/A");
                customer = customerModel.Data as GetCustomerDto;
            }

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

            // Get completedCycles from card (AvailableRewardCount is populated by PopulateFreeRewardFieldsAsync)
            int completedCycles = card.MaxSpendCount > 0 ? card.MaxSpendCount : 
                                  (card.AvailableRewardCount > 0 ? card.AvailableRewardCount : 
                                   card.CompletedCycles);
            
            string? freeRewardLabel = card.FreeRewardLabel;
            if (string.IsNullOrWhiteSpace(freeRewardLabel) && tpl != null)
            {
                freeRewardLabel = string.IsNullOrWhiteSpace(tpl.FreeRewardLabel) ? "bonus" : tpl.FreeRewardLabel;
            }

            return Ok(new
            {
                success = true,
                customerFullName = (customer?.FirstName + " " + customer?.LastName)?.Trim(),
                cardNumber = card.CardNumber,
                currentPoints = card.CurrentPoints,
                completedCycles = completedCycles,  // Available reward count
                availableRewardCount = completedCycles,  // = CompletedCycles
                maxSpendCount = completedCycles,  // = CompletedCycles (UI input limit)
                freeRewardLabel = freeRewardLabel ?? "bonus",  // From card DTO or template
                currency = card.Currency ?? tpl?.Currency ?? "AZN",  // Currency from card's CardTemplate
                tierName,
                tierPercent,
                tierPercentCash,
                tierIconUrl
            });
        }

        // === START REDEEM ===
        public sealed record StartRedeemRequest( string Code, int Delta, string? OrderId,string mode,string? paymentMethod, int? spendCount);

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Start([FromBody] StartRedeemRequest model, CancellationToken ct)
        {
            _logger.LogInformation("Start redeem called: ProgramId={ProgramId}, Code={Code}, Delta={Delta}, Mode={Mode}",
               model.Code, model.Delta, model.mode);

            // If mode is "pay", ensure delta is negative (convert positive to negative)
            // If mode is "earn", ensure delta is positive (convert negative to positive)
            int delta = model.Delta;
            if (string.Equals(model.mode, "pay", StringComparison.OrdinalIgnoreCase))
            {
                if (delta > 0)
                {
                    _logger.LogInformation("Mode is 'pay' but Delta is positive. Converting to negative. Original Delta={Delta}", delta);
                    delta = -delta;
                }
            }
            else if (string.Equals(model.mode, "earn", StringComparison.OrdinalIgnoreCase))
            {
                if (delta < 0)
                {
                    _logger.LogInformation("Mode is 'earn' but Delta is negative. Converting to positive. Original Delta={Delta}", delta);
                    delta = -delta; // Convert negative to positive
                }
            }

            var tenantId = _tenantProvider.CurrentTenantId;
            var programId = _tenantProvider.CurrentProgramId;
            if (tenantId is null)
            {
                _logger.LogWarning("Redeem Start failed: TenantId is null");
                return BadRequest(new { success = false, message = "Tenant subdomendən təyin olunmadı." });
            }

            var code = (model.Code ?? "").Trim();
            GetLoyaltyCardDto? card = null;
            GetCustomerDto? customer = null;

            // First, try to find by barcode (priority)
            var cardModel = await _cardSvc.GetByBarcodeValueAsync(tenantId.Value, code);
            card = cardModel.Data as GetLoyaltyCardDto;

            if (card == null && long.TryParse(code, out long walveroId))
            {
                // Barcode not found, try WalveroId
                _logger.LogInformation("Barcode not found, trying WalveroId: {WalveroId}", walveroId);
                
                // Find customer by WalveroId
                var customerEntity = await _customerRepository.GetByWalveroIdAsync(tenantId.Value, walveroId, ct);
                
                if (customerEntity == null)
                {
                    _logger.LogWarning("Customer not found by WalveroId: {WalveroId} for TenantId: {TenantId}", walveroId, tenantId.Value);
                    return BadRequest(new { success = false, message = $"Kart tapılmadı (barcode və WalveroId uyğun gəlmədi)." });
                }

                // Get customer DTO
                var customerModel = await _customerSvc.GetByIdName(customerEntity.Id, "N/A");
                customer = customerModel.Data as GetCustomerDto;

                if (customer == null)
                {
                    _logger.LogWarning("Customer DTO not found for CustomerId: {CustomerId}", customerEntity.Id);
                    return BadRequest(new { success = false, message = "Customer məlumatları tapılmadı." });
                }

                // Find the active card for this customer and program
                if (programId.HasValue && customer.LoyaltyCards != null && customer.LoyaltyCards.Any())
                {
                    card = customer.LoyaltyCards
                        .Where(c => c.LoyaltyProgramId == programId.Value)
                        .OrderByDescending(c => c.Id)
                        .FirstOrDefault();
                }

                if (card == null)
                {
                    _logger.LogWarning("Active card not found for CustomerId: {CustomerId}, ProgramId: {ProgramId}", customer.Id, programId);
                    return BadRequest(new { success = false, message = "Aktiv kart tapılmadı." });
                }
            }
            else if (card == null)
            {
                // Neither barcode nor WalveroId found
                _logger.LogWarning("Card not found by barcode: {Code}", code);
                return BadRequest(new { success = false, message = "Kart tapılmadı." });
            }
            else
            {
                // Card found by barcode, get customer
                var customerModel = await _customerSvc.GetByIdName(card.CustomerId, "N/A");
                customer = customerModel.Data as GetCustomerDto;
            }

            var programModel = await _programSvc.Get(programId.Value);
            var program = programModel.Data as GetLoyaltyProgramDto;
            var tpl = program.CardTemplate;

            string? tierName = null; int? tierPercent = null; int? tierPercentCash = null; string? tierIconUrl = null;
            // Percent hesaplaması ProgressBased olmayan template'ler üçün (PointsBased və TierBased)
            if (tpl.TemplateType != CardTemplateType.ProgressBased)
            {
                if (tpl.OneRedeem == true)
                    (tierName, tierPercent, tierPercentCash, tierIconUrl) = TierResolverDouble(delta, tpl, AbsHttps);
                else
                    (tierName, tierPercent, tierPercentCash, tierIconUrl) = TierResolver(card.CurrentPoints, tpl, AbsHttps);
            }
           
            int? percent=model.paymentMethod=="cash" ? tierPercentCash : tierPercent;
            if (delta > 0 && percent.HasValue) {
                delta = delta * percent.Value / 100;
            }
          


            var dto = new RedeemRequestDto
            {
                LoyaltyProgramId = programId,
                Code = (model.Code ?? "").Trim(),
                Delta = delta,
                OrderId = model.OrderId,
                TenantId = tenantId.Value,
                SpendCount = model.spendCount // Add SpendCount for "Xərclə" mode
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
            if (string.IsNullOrWhiteSpace(urlOrPath))
                return null;

            // scheme-relative: //example.com/path
            if (urlOrPath.StartsWith("//", StringComparison.Ordinal))
                urlOrPath = "https:" + urlOrPath;

            // Absolute URL -> force https
            if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var absolute))
            {
                if (absolute.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                    return absolute.ToString();

                return new UriBuilder(absolute) { Scheme = "https", Port = -1 }.Uri.ToString();
            }

            // Relative -> build from PublicBaseUrl
            var publicBaseUrl = "https://walvero.com"; // inject IConfiguration _configuration

            if (string.IsNullOrWhiteSpace(publicBaseUrl) ||
                !Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var baseUri))
            {
                // fallback (istəsən tamamilə throw da edə bilərsən)
                var host = Request.Headers["X-Forwarded-Host"].ToString();
                if (string.IsNullOrWhiteSpace(host))
                    host = Request.Host.Value;

                host = host.Split(',')[0].Trim();

                if (string.IsNullOrWhiteSpace(host) || host.Contains(" ") || host.Contains("|") || host.Contains("/") || host.Contains("\\"))
                    throw new InvalidOperationException($"Invalid host for URL building: '{host}'");

                baseUri = new Uri($"https://{host}");
            }

            var rel = urlOrPath.TrimStart('/', '\\');
            return new Uri(baseUri, rel).ToString();
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
