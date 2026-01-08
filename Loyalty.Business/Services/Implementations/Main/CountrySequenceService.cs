using Dapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
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
                // Use raw SQL to query with IsActive filter since IsActive is ignored in DbContext configuration
                var sql = @"
                    SELECT 
                        CountryCode,
                        Name,
                        Code,
                        Flag
                    FROM CountrySequences
                    WHERE IsActive = 1
                    ORDER BY CountryCode
                ";

                var connection = _dbContext.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync(ct);

                var countries = await connection.QueryAsync<GetCountrySequenceDto>(
                    new CommandDefinition(
                        sql,
                        commandTimeout: 30,
                        cancellationToken: ct
                    )
                );

                return new ServiceResult<List<GetCountrySequenceDto>>(true, countries.ToList());
            }
            catch (Exception ex)
            {
                return new ServiceResult<List<GetCountrySequenceDto>>(false, default, $"Error retrieving active countries: {ex.Message}");
            }
        }
    }
}

