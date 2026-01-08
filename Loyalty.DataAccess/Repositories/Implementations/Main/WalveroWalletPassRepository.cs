using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class WalveroWalletPassRepository : GenericRepository<WalveroWalletPass>, IWalveroWalletPassRepository
    {
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly ILogger<WalveroWalletPassRepository>? _logger;

        public WalveroWalletPassRepository(
            LoyaltyWalletDbContext dbContext,
            ILogger<WalveroWalletPassRepository>? logger = null) : base(dbContext)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        private async Task SaveChangesAsync(CancellationToken ct = default)
        {
            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException dbEx)
            {
                // Log inner exception for better debugging
                var innerEx = dbEx.InnerException;
                if (innerEx != null)
                {
                    _logger?.LogError(innerEx, 
                        "Database update error: {Message}. Inner exception: {InnerMessage}", 
                        dbEx.Message, innerEx.Message);
                    
                    // If it's a SQL exception, log the error number
                    if (innerEx is SqlException sqlEx)
                    {
                        _logger?.LogError("SQL Error Number: {ErrorNumber}, State: {State}", 
                            sqlEx.Number, sqlEx.State);
                    }
                }
                else
                {
                    _logger?.LogError(dbEx, "Database update error: {Message}", dbEx.Message);
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error saving WalveroWalletPass: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<WalveroWalletPass> GetOrCreateAsync(
            int applicationUserId,
            int tenantId,
            string walveroIdMasked,
            CancellationToken ct = default)
        {
            var existing = await GetByUserAndTenantAsync(applicationUserId, tenantId, ct);
            if (existing != null)
            {
                return existing;
            }

            var newPass = new WalveroWalletPass
            {
                ApplicationUserId = applicationUserId,
                TenantId = tenantId,
                WalveroIdMasked = walveroIdMasked,
                HasIosPass = false,
                HasAndroidPass = false,
                IsActive = true
            };

            await AddAsync(newPass);
            await SaveChangesAsync(ct);

            return newPass;
        }

        public async Task<WalveroWalletPass?> GetByUserAndTenantAsync(
            int applicationUserId,
            int tenantId,
            CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .FirstOrDefaultAsync(
                    p => p.ApplicationUserId == applicationUserId && p.TenantId == tenantId,
                    ct);
        }

        public async Task UpdatePassUrlsAsync(
            int passId,
            string? applePassUrl,
            string? googleSaveUrl,
            string? appleSerialNumber,
            string? googleObjectId,
            bool hasIosPass,
            bool hasAndroidPass,
            CancellationToken ct = default)
        {
            var pass = await GetByIdAsync(passId);
            if (pass == null)
            {
                throw new InvalidOperationException($"WalveroWalletPass with Id {passId} not found");
            }

            pass.ApplePassUrl = applePassUrl;
            pass.GoogleSaveUrl = googleSaveUrl;
            pass.AppleSerialNumber = appleSerialNumber;
            pass.GoogleObjectId = googleObjectId;
            pass.HasIosPass = hasIosPass;
            pass.HasAndroidPass = hasAndroidPass;
            pass.LastGeneratedAt = DateTime.UtcNow;

            Update(pass);
            await SaveChangesAsync(ct);
        }
    }
}

