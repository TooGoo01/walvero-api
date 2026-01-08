using Dapper;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Repositories.Abstractions.Sequences;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Loyalty.DataAccess.Repositories.Implementations.Sequences
{
    /// <summary>
    /// Repository for atomic sequence generation using SQL Server UPDATE...OUTPUT
    /// Uses UPDLOCK + HOLDLOCK to prevent race conditions
    /// </summary>
    public class SequenceRepository : ISequenceRepository
    {
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly ILogger<SequenceRepository>? _logger;

        public SequenceRepository(LoyaltyWalletDbContext dbContext, ILogger<SequenceRepository>? logger = null)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger;
        }

        private IDbConnection GetConnection()
        {
            return _dbContext.Database.GetDbConnection();
        }

        public async Task<(int LastNumber, string Prefix)> GetNextTenantSequenceAsync(int tenantId, CancellationToken ct = default)
        {
            const string sql = @"
                DECLARE @OutputTable TABLE (LastNumber INT, Prefix NVARCHAR(5));

                -- Ensure row exists (idempotent insert)
                IF NOT EXISTS (SELECT 1 FROM TenantCustomerSequences WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @TenantId)
                BEGIN
                    INSERT INTO TenantCustomerSequences (TenantId, LastNumber, Prefix, UpdatedAt)
                    VALUES (@TenantId, 0, '789', GETUTCDATE());
                END

                -- Atomically increment and return
                -- Prefix rollover: if LastNumber >= 9999, increment prefix (789 -> 790 -> 791...)
                UPDATE TenantCustomerSequences WITH (UPDLOCK, HOLDLOCK)
                SET 
                    LastNumber = CASE 
                        WHEN LastNumber >= 9999 THEN 1 
                        ELSE LastNumber + 1 
                    END,
                    Prefix = CASE 
                        WHEN LastNumber >= 9999 THEN CAST(CAST(Prefix AS INT) + 1 AS NVARCHAR(5))
                        ELSE Prefix
                    END,
                    UpdatedAt = GETUTCDATE()
                OUTPUT INSERTED.LastNumber, INSERTED.Prefix
                INTO @OutputTable (LastNumber, Prefix)
                WHERE TenantId = @TenantId;

                SELECT LastNumber, Prefix FROM @OutputTable;
            ";

            try
            {
                var connection = GetConnection();
                if (connection.State != ConnectionState.Open)
                    connection.Open();

                var result = await connection.QueryFirstOrDefaultAsync<(int LastNumber, string Prefix)>(
                    sql,
                    new { TenantId = tenantId },
                    commandTimeout: 30
                );

                if (result.LastNumber == 0 && string.IsNullOrEmpty(result.Prefix))
                {
                    // Fallback: try again or use default
                    _logger?.LogWarning("Sequence generation returned default values for TenantId: {TenantId}", tenantId);
                    result = (1, "789");
                }

                _logger?.LogDebug("Generated sequence for TenantId {TenantId}: LastNumber={LastNumber}, Prefix={Prefix}",
                    tenantId, result.LastNumber, result.Prefix);

                return result;
            }
            catch (SqlException ex) when (ex.Number == 1205) // Deadlock
            {
                _logger?.LogWarning(ex, "Deadlock detected for TenantId {TenantId}, retrying...", tenantId);
                // Retry once
                await Task.Delay(100, ct);
                return await GetNextTenantSequenceAsync(tenantId, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating sequence for TenantId {TenantId}", tenantId);
                throw;
            }
        }

        public async Task<int> GetNextCountrySequenceAsync(int countryCode, CancellationToken ct = default)
        {
            const string sql = @"
                DECLARE @OutputTable TABLE (LastNumber INT);

                -- Ensure row exists (idempotent insert)
                IF NOT EXISTS (SELECT 1 FROM CountrySequences WITH (UPDLOCK, HOLDLOCK) WHERE CountryCode = @CountryCode)
                BEGIN
                    INSERT INTO CountrySequences (CountryCode, LastNumber, UpdatedAt)
                    VALUES (@CountryCode, 0, GETUTCDATE());
                END

                -- Atomically increment and return
                UPDATE CountrySequences WITH (UPDLOCK, HOLDLOCK)
                SET 
                    LastNumber = LastNumber + 1,
                    UpdatedAt = GETUTCDATE()
                OUTPUT INSERTED.LastNumber
                INTO @OutputTable (LastNumber)
                WHERE CountryCode = @CountryCode;

                SELECT LastNumber FROM @OutputTable;
            ";

            try
            {
                var connection = GetConnection();
                if (connection.State != ConnectionState.Open)
                    connection.Open();

                var result = await connection.QueryFirstOrDefaultAsync<int>(
                    sql,
                    new { CountryCode = countryCode },
                    commandTimeout: 30
                );

                if (result == 0)
                {
                    _logger?.LogWarning("Sequence generation returned 0 for CountryCode: {CountryCode}", countryCode);
                    result = 1;
                }

                _logger?.LogDebug("Generated sequence for CountryCode {CountryCode}: LastNumber={LastNumber}",
                    countryCode, result);

                return result;
            }
            catch (SqlException ex) when (ex.Number == 1205) // Deadlock
            {
                _logger?.LogWarning(ex, "Deadlock detected for CountryCode {CountryCode}, retrying...", countryCode);
                await Task.Delay(100, ct);
                return await GetNextCountrySequenceAsync(countryCode, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating sequence for CountryCode {CountryCode}", countryCode);
                throw;
            }
        }
    }
}

