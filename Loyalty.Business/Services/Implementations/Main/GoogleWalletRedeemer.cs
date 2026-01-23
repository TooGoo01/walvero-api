using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class GoogleWalletRedeemer
    {
        private readonly IGoogleWalletServiceFactory _factory;
        private readonly IUnitOfWork _uow;
        private readonly ILoyaltyCardRepository _cardRepository;
        public GoogleWalletRedeemer(IGoogleWalletServiceFactory factory, IUnitOfWork uow, ILoyaltyCardRepository cardRepository)
        {
            _factory = factory; _uow = uow;
            _cardRepository = cardRepository;
        }

        public async Task<ServiceResult> RedeemByCardIdAsync(
            int cardId, CardTemplateSnapshot tpl, int pts, int steps, int delta, CancellationToken ct)
        {
            // card → tenantId, programId tap
            var card = await _cardRepository.GetById(cardId, ct);
            if (card is null) return new ServiceResult(false, "Card not found.");
            var cardholderName = card.Customer.FirstName + " " + card.Customer.LastName;

            var gw = await _factory.CreateForTenantAsync(card.TenantId, card.LoyaltyProgramId, ct);
            return await gw.RedeemByCardIdAsync(cardId, tpl, pts, steps, delta,cardholderName, ct);
        }
    }
}
