using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Implementations.Main;
using Loyalty.DataAccess.UnitOfWorks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class LoyaltyCardService : ILoyaltyCardService
    {
        private readonly ILoyaltyCardRepository _repo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public LoyaltyCardService(ILoyaltyCardRepository repo, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _repo = repo;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public async Task<string> GenerateUniqueWalletCodeAsync(int tenantId, int programId, int userId, CancellationToken ct = default)
        {
            const string PREFIX = "789";
            var tenantPart = $"{PREFIX}{tenantId:D2}";
            var userPart = $"{PREFIX}{userId:D4}";
            // Tam kod: 5 + 7 = 12 rəqəm
            var code = tenantPart + userPart; // məsələn: "789017890001"
            var exists = await _repo.ExistsAsync(tenantId, code);
            if (exists)
                throw new InvalidOperationException(
                    $"Wallet code already exists for tenant {tenantId}: {code}");

            return code;
        }

        public async Task<ServiceResult> GetByBarcodeValueAsync(int tenantId, string code)
        {
            try
            {
                var result = await _repo.GetByBarcodeValueAsync(tenantId,code);
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = _mapper.Map<GetLoyaltyCardDto>(result);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }
    }
}
