using System.Diagnostics;
using System.Text;
using Loyalty.Business.Helpers;
using Loyalty.Business.Models.TenantIntegration;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Implementations.Main;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// r_keeper 7 FarCards HTTP (ExtDllHTTP.dll) endpoint-ləri. ExtDllHTTP.ini:
    ///   [Server] Address=https://appapi.walvero.com
    ///   GetCardInfoEx=api/rkeeper/{walletToken}/getcardinfoex
    ///   TransactionsEx=api/rkeeper/{walletToken}/transactionsex
    /// ExtDllHTTP öz auth header-i göndərmir → tenant inteqrasiyanın wallet token-i URL path-dədir
    /// (DB-də yalnız hash). Kassa sinxron gözləyir — cavab həmişə HTTP 200 + FarCards XML (Result 0/1).
    /// </summary>
    [ApiController]
    [Route("api/rkeeper/{token}")]
    [AllowAnonymous]
    [RequestSizeLimit(1_000_000)]
    public class RkeeperFarCardsController : ControllerBase
    {
        private const string XmlContentType = "text/xml; charset=utf-8";

        private readonly ITenantIntegrationResolver _resolver;
        private readonly ITenantStatusGuard _tenantStatusGuard;
        private readonly IRkeeperLoyaltyService _rkeeperService;
        private readonly ILogger<RkeeperFarCardsController> _logger;

        public RkeeperFarCardsController(
            ITenantIntegrationResolver resolver,
            ITenantStatusGuard tenantStatusGuard,
            IRkeeperLoyaltyService rkeeperService,
            ILogger<RkeeperFarCardsController> logger)
        {
            _resolver = resolver;
            _tenantStatusGuard = tenantStatusGuard;
            _rkeeperService = rkeeperService;
            _logger = logger;
        }

        [HttpPost("getcardinfoex")]
        public async Task<IActionResult> GetCardInfoEx(string token, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var ctx = await ResolveAsync(token, ct);
                if (ctx == null)
                    return Xml(FarCardsXml.BuildCardNotFound());

                var body = await ReadBodyAsync(ct);
                var xml = await _rkeeperService.GetCardInfoAsync(ctx, body, ct);
                _logger.LogInformation("rkeeper GetCardInfoEx DONE. TenantId={TenantId} ElapsedMs={Elapsed}", ctx.TenantId, sw.ElapsedMilliseconds);
                return Xml(xml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "rkeeper GetCardInfoEx unhandled error. ElapsedMs={Elapsed}", sw.ElapsedMilliseconds);
                return Xml(FarCardsXml.BuildCardNotFound());
            }
        }

        [HttpPost("transactionsex")]
        public async Task<IActionResult> TransactionsEx(string token, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            string? body = null;
            try
            {
                var ctx = await ResolveAsync(token, ct);
                if (ctx == null)
                    return Xml(FarCardsXml.BuildTransactionsError(401, "Walvero: integration not found or inactive"));

                body = await ReadBodyAsync(ct);
                _logger.LogInformation("rkeeper TransactionsEx RAW BODY. TenantId={TenantId} Body={Body}", ctx.TenantId, body);

                var xml = await _rkeeperService.ProcessTransactionsAsync(ctx, body, ct);
                _logger.LogInformation("rkeeper TransactionsEx DONE. TenantId={TenantId} ElapsedMs={Elapsed} Response={Response}",
                    ctx.TenantId, sw.ElapsedMilliseconds, xml);
                return Xml(xml);
            }
            catch (Exception ex)
            {
                // Result=1 → kassa çeki bağlamır / kassir təkrar cəhd edir; tranzaksiya yarımçıq qalmır (DB commit atomikdir)
                _logger.LogError(ex, "rkeeper TransactionsEx unhandled error. ElapsedMs={Elapsed} Body={Body}", sw.ElapsedMilliseconds, body);
                return Xml(FarCardsXml.BuildTransactionsError(500, "Walvero: internal error, retry"));
            }
        }

        /// <summary>E-poçtla hesab axtarışı dəstəklənmir — Result=1 (tapılmadı).</summary>
        [HttpPost("findemail")]
        public IActionResult FindEmail(string token) =>
            Xml("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Root><FindEmail Result=\"1\"/></Root>");

        /// <summary>FarCards lisenziya xətası bildirişi — yalnız log.</summary>
        [HttpPost("licenseinfo")]
        public async Task<IActionResult> LicenseInfo(string token, CancellationToken ct)
        {
            var body = await ReadBodyAsync(ct);
            _logger.LogWarning("rkeeper LicenseInfo: {Body}", body);
            return Xml("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Root/>");
        }

        /// <summary>Quraşdırma yoxlaması (brauzerdən açılır): token etibarlıdırmı.</summary>
        [HttpGet("ping")]
        public async Task<IActionResult> Ping(string token, CancellationToken ct)
        {
            var ctx = await ResolveAsync(token, ct);
            return ctx == null
                ? NotFound(new { success = false, message = "Integration not found or inactive" })
                : Ok(new { success = true, tenantId = ctx.TenantId, integrationId = ctx.TenantIntegrationId });
        }

        private async Task<WalletWebhookResolvedContext?> ResolveAsync(string token, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var ctx = await _resolver.ResolveByWalletTokenAsync(token.Trim(), ct);
            if (ctx == null || !string.Equals(ctx.Source, RkeeperLoyaltyService.Source, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("rkeeper: token resolve olunmadı və ya source rkeeper deyil. Source={Source}", ctx?.Source);
                return null;
            }

            if (!await _tenantStatusGuard.IsActiveAsync(ctx.TenantId, ct))
            {
                _logger.LogWarning("rkeeper blocked — tenant inactive. TenantId={TenantId}", ctx.TenantId);
                return null;
            }

            return ctx;
        }

        private async Task<string> ReadBodyAsync(CancellationToken ct)
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            return await reader.ReadToEndAsync(ct);
        }

        private ContentResult Xml(string xml) => Content(xml, XmlContentType, Encoding.UTF8);
    }
}
