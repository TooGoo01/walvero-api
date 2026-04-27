using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class AppleWalletRedeemer : IWalletRedeemer
    {
        private readonly IAppleWalletFactory _factory;              // hazırda istifadə olunmur, amma saxlayıram
        private readonly IAppleWalletService _apple;
        private readonly IWalletCredentialProvider _provider;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<AppleWalletRedeemer> _logger;      // <-- LOGGER
        
        public AppleWalletRedeemer(
            IAppleWalletFactory factory,
            IUnitOfWork uow,
            IAppleWalletService apple,
            IWalletCredentialProvider provider,
            ILogger<AppleWalletRedeemer> logger)                    // <-- LOG inject
        {
            _factory = factory;
            _uow = uow;
            _apple = apple;
            _provider = provider;
            _logger = logger;
        }

        public async Task<ServiceResult> RedeemByCardIdAsync(
          int id, CardTemplateSnapshot tpl, int pts, int steps, int delta, CancellationToken ct = default,
          int lotteryTickets = 0, string? lotteryWinPrize = null, DateTime? lotteryDrawDate = null)
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["CardId"] = id
            });

            var sw = Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("Apple redeem started. Delta={Delta}, Points={Points}, Steps={Steps}, TemplateType={TemplateType}",
                    delta, pts, steps, tpl.TemplateType);

                // 1) Card → tenantId tap
                var card = await _uow.Repository<LoyaltyCard>().GetByIdAsync(id);
                if (card is null)
                {
                    _logger.LogWarning("Card not found. CardId={CardId}", id);
                    return new ServiceResult(false, "Card not found.");
                }

                // scope-a TenantId əlavə edək (loglarda görünsün)
                using var scope2 = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["TenantId"] = card.TenantId
                });

                // 2) Tenant-a görə Apple cred-lərini götür
                _logger.LogDebug("Fetching Apple credentials for TenantId={TenantId}", card.TenantId);
                var creds = await _provider.GetAppleAsync(card.TenantId, ct);
                if (creds is null)
                {
                    _logger.LogWarning("Apple credentials not found for TenantId={TenantId}", card.TenantId);
                    return new ServiceResult(false, "Apple credentials not found.");
                }
                _logger.LogDebug("Fetching Apple credentials for P12={TenantId}", creds.CertP12Path);
                _logger.LogDebug("Fetching Apple credentials for WWdr={TenantId}", creds.WwdrCertPath);
                // 3) AppleWalletService çağırışı
                _logger.LogInformation("Routing to AppleWalletService.RedeemByCardIdAsync...");
                var res = await _apple.RedeemByCardIdAsync(id, tpl, creds, pts, steps, delta, ct, lotteryTickets, lotteryWinPrize, lotteryDrawDate);

                sw.Stop();
                if (res.Success)
                {
                    _logger.LogInformation("Apple redeem succeeded in {ElapsedMs} ms.", sw.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogWarning("Apple redeem failed in {ElapsedMs} ms. Message={Message}", sw.ElapsedMilliseconds, res.Message);
                }

                return res;
            }
            catch (InvalidOperationException ex)
            {
                sw.Stop();
                _logger.LogWarning(ex, "Apple Wallet credentials not found for TenantId={TenantId}. ElapsedMs={ElapsedMs}. Message={Message}",
                    id, sw.ElapsedMilliseconds, ex.Message);
                return new ServiceResult(false, ex.Message);
            }
            catch (FileNotFoundException ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Apple Wallet certificate file not found for CardId={CardId}. ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);
                return new ServiceResult(false, "Apple Wallet certificate file not found.");
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Apple Wallet HTTP error for CardId={CardId}. ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);
                return new ServiceResult(false, "Apple Wallet communication error.");
            }
            catch (IOException ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Apple Wallet I/O error for CardId={CardId}. ElapsedMs={ElapsedMs}",
                    id, sw.ElapsedMilliseconds);
                return new ServiceResult(false, "Apple Wallet file operation error.");
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _logger.LogWarning("Apple redeem canceled after {ElapsedMs} ms.", sw.ElapsedMilliseconds);
                return new ServiceResult(false, "Operation canceled.");
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Apple redeem threw unexpected exception after {ElapsedMs} ms.", sw.ElapsedMilliseconds);
                return new ServiceResult(false, "Apple redeem failed due to an unexpected error.");
            }
        }
    }
}
