using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Sequences;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Service for retrieving country sequence information
    /// </summary>
    public class CountrySequenceService : ICountrySequenceService
    {
        private readonly LoyaltyWalletDbContext _dbContext;

        public CountrySequenceService(LoyaltyWalletDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ServiceResult<List<GetCountrySequenceDto>>> GetActiveCountriesAsync(CancellationToken ct = default)
        {
            try
            {
              
                var countries = await _dbContext.CountrySequences.Where(x => x.IsActive)
                    .Select(cs => new GetCountrySequenceDto
                    {
                        CountryCode = cs.CountryCode,
                        Name = cs.Name,
                        Code = cs.Code,
                        Flag = cs.Flag
                    })
                    .OrderBy(cs => cs.CountryCode)
                    .ToListAsync(ct);

                return new ServiceResult<List<GetCountrySequenceDto>>(true, countries);
            }
            catch (Exception ex)
            {
                return new ServiceResult<List<GetCountrySequenceDto>>(false, default, $"Error retrieving active countries: {ex.Message}");
            }
        }
    }
}

