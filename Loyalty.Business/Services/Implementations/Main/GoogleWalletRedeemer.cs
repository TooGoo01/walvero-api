using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class GoogleWalletRedeemer
    {
        private readonly IGoogleWalletServiceFactory _factory;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<GoogleWalletRedeemer> _logger;

        public GoogleWalletRedeemer(IGoogleWalletServiceFactory factory, IUnitOfWork uow, ILogger<GoogleWalletRedeemer> logger)
        {
            _factory = factory;
            _uow = uow;
            _logger = logger;
        }

        public async Task<ServiceResult> RedeemByCardIdAsync(
            int cardId, CardTemplateSnapshot tpl, int pts, int steps, int delta, CancellationToken ct)
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?> { ["CardId"] = cardId });
            var sw = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Google redeem started. Delta={Delta}, Points={Points}, Steps={Steps}, TemplateType={TemplateType}",
                    delta, pts, steps, tpl.TemplateType);

                // card → tenantId, programId tap
                var card = await _uow.Repository<LoyaltyCard>().GetByIdAsync(cardId);
                if (card is null)
                {
                    _logger.LogWarning("Card not found. CardId={CardId}", cardId);
                    return new ServiceResult(false, "Card not found.");
                }

                using var scope2 = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["TenantId"] = card.TenantId,
                    ["ProgramId"] = card.LoyaltyProgramId
                });

                _logger.LogDebug("Creating Google Wallet service for TenantId={TenantId}, ProgramId={ProgramId}",
                    card.TenantId, card.LoyaltyProgramId);

                var gw = await _factory.CreateForTenantAsync(card.TenantId, card.LoyaltyProgramId, ct);
                var cardholderName = $"{card.Customer?.FirstName} {card.Customer?.LastName}".Trim();

                _logger.LogInformation("Routing to GoogleWalletService.RedeemByCardIdAsync... Cardholder={Cardholder}", cardholderName);
                var res = await gw.RedeemByCardIdAsync(cardId, tpl, pts, steps, delta,cardholderName, ct);

                sw.Stop();
                if (res.Success)
                {
                    _logger.LogInformation("Google redeem succeeded in {ElapsedMs} ms.", sw.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogWarning("Google redeem failed in {ElapsedMs} ms. Message={Message}",
                        sw.ElapsedMilliseconds, res.Message);
                }

                return res;
            }
            catch (InvalidOperationException ex)
            {
                sw.Stop();
                _logger.LogWarning(ex, "Google Wallet credentials or program not found for CardId={CardId}. ElapsedMs={ElapsedMs}. Message={Message}",
                    cardId, sw.ElapsedMilliseconds, ex.Message);
                return new ServiceResult(false, ex.Message);
            }
            catch (FileNotFoundException ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Google Wallet service account file not found for CardId={CardId}. ElapsedMs={ElapsedMs}",
                    cardId, sw.ElapsedMilliseconds);
                return new ServiceResult(false, "Google Wallet configuration file not found.");
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Google Wallet API HTTP error for CardId={CardId}. ElapsedMs={ElapsedMs}",
                    cardId, sw.ElapsedMilliseconds);
                return new ServiceResult(false, "Google Wallet API communication error.");
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                _logger.LogWarning("Google redeem canceled after {ElapsedMs} ms.", sw.ElapsedMilliseconds);
                return new ServiceResult(false, "Operation canceled.");
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Google redeem threw unexpected exception after {ElapsedMs} ms.", sw.ElapsedMilliseconds);
                return new ServiceResult(false, "Google redeem failed due to an unexpected error.");
            }
        }
    }
}
