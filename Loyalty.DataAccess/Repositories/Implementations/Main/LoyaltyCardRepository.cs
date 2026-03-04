using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class LoyaltyCardRepository : GenericRepository<LoyaltyCard>, ILoyaltyCardRepository
    {
        public LoyaltyCardRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }
        public Task<LoyaltyCard?> GetByCardNumberAsync(int tenantId, string cardNumber, CancellationToken ct = default)
         => GetAsQueryable().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CardNumber == cardNumber, ct);
        public Task<LoyaltyCard?> GetByBarcodeValueAsync(int tenantId, string cardNumber, CancellationToken ct = default)
         => GetAsQueryable().Include(x=>x.Customer).ThenInclude(c => c.applicationUser).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.BarcodeValue == cardNumber, ct);

        public Task<bool> ExistsAsync(int tenantId, string cardNumber, CancellationToken ct = default)
            => GetAsQueryable().AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.CardNumber == cardNumber, ct);

        public Task<List<LoyaltyCard>> ListByCustomerAsync(int tenantId, int customerId, CancellationToken ct = default)
            => GetAsQueryable().AsNoTracking()
                 .Where(x => x.TenantId == tenantId && x.CustomerId == customerId)
                 .OrderByDescending(x => x.Id)
                 .ToListAsync(ct);

        public Task<LoyaltyCard?> GetLastActiveByCustomerAsync(int tenantId, int programId, int customerId, CancellationToken ct)
        {
            return GetAsQueryable()
                .Where(x => x.TenantId == tenantId && 
                           x.CustomerId == customerId && 
                           x.LoyaltyProgramId == programId &&
                           x.IsActive)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct);
        }

        public Task<LoyaltyCard?> GetLastInactiveByCustomerAsync(int tenantId, int programId, int customerId, CancellationToken ct)
        {
            return GetAsQueryable()
                .Where(x => x.TenantId == tenantId &&
                            x.CustomerId == customerId &&
                            x.LoyaltyProgramId == programId &&
                            !x.IsActive)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct);
        }

        public Task<LoyaltyCard?> GetById(int id, CancellationToken ct)
        {
            return GetAsQueryable().Include(x=>x.Customer).ThenInclude(x=>x.applicationUser).FirstOrDefaultAsync(x => x.Id==id, ct);
        }

        public async Task<List<LoyaltyCard>> GetLastActiveCardsForApplicationUserAsync(
      int applicationUserId,
      string? name,
      int? categoryId,
      CancellationToken ct = default)
        {
            // 1) Əsas sorğu – user-in aktiv kartları (hidden tenant-ləri istisna et)
            var baseQuery = GetAsQueryable()
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    c.Customer != null &&
                    c.Customer.applicationUserId == applicationUserId &&
                    c.Customer.IsActive &&
                    c.Status == CardStatus.Active &&
                    !c.Tenant.IsHidden);

            // 1.1) Mağaza adına görə filter (Tenant.CompanyName)
            if (!string.IsNullOrWhiteSpace(name))
            {
                baseQuery = baseQuery.Where(c =>
                    EF.Functions.Like(c.Tenant.CompanyName, $"%{name}%"));
                // əgər Tenant navigation yoxdursa, belə et:
                // baseQuery = baseQuery.Where(c =>
                //     EF.Functions.Like(c.LoyaltyProgram.Tenant.CompanyName, $"%{name}%"));
            }

            // 1.2) Kateqoriyaya görə filter
            if (categoryId.HasValue)
            {
                baseQuery = baseQuery.Where(c => c.Tenant.categoryId == categoryId.Value);
                // yenə də Tenant yoxdursa:
                // baseQuery = baseQuery.Where(c => c.LoyaltyProgram.Tenant.categoryId == categoryId.Value);
            }

            // 2) Hər (TenantId, LoyaltyProgramId) üçün son kartın Id-sini tap
            var lastCardIdsQuery =
                from card in baseQuery
                group card by new { card.TenantId, card.LoyaltyProgramId } into g
                select g
                    .OrderByDescending(x => x.Id) // CreatedAt varsa onu istifadə et
                    .First().Id;

            var lastCardIds = await lastCardIdsQuery.ToListAsync(ct);

            if (!lastCardIds.Any())
                return new List<LoyaltyCard>();

            // 3) İndi həmin kartları full include ilə götür (Customer, Program və s.)
            var cards = await GetAsQueryable()
                .AsNoTracking()
                .Where(c => lastCardIds.Contains(c.Id))
                .Include(c => c.Customer)
                .Include(c => c.LoyaltyProgram)
                    .ThenInclude(p => p.CardTemplate)
                .Include(c => c.Tenant)
                .ToListAsync(ct);

            return cards;
        }

        // OPTIMIZATION: Separate count method to avoid loading all cards into memory
        public async Task<int> GetActiveCardsCountForApplicationUserAsync(
            int applicationUserId,
            string? name,
            int? categoryId,
            int? countryId,
            int? cityId,
            CancellationToken ct = default)
        {
            var baseQuery = GetAsQueryable()
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    c.Customer != null &&
                    c.Customer.applicationUserId == applicationUserId &&
                    c.Customer.IsActive &&
                    c.Status == CardStatus.Active &&
                    !c.Tenant.IsHidden);

            if (!string.IsNullOrWhiteSpace(name))
            {
                baseQuery = baseQuery.Where(c =>
                    EF.Functions.Like(c.Tenant.CompanyName, $"%{name}%"));
            }

            if (categoryId.HasValue)
            {
                baseQuery = baseQuery.Where(c => c.Tenant.categoryId == categoryId.Value);
            }

            if (countryId.HasValue)
            {
                baseQuery = baseQuery.Where(c => 
                    c.Tenant.TenantLocation != null && 
                    c.Tenant.TenantLocation.countryId == countryId.Value);
            }

            if (cityId.HasValue)
            {
                baseQuery = baseQuery.Where(c => 
                    c.Tenant.TenantLocation != null && 
                    c.Tenant.TenantLocation.cityId == cityId.Value);
            }

            // Count distinct (TenantId, LoyaltyProgramId) combinations
            var count = await (
                from card in baseQuery
                group card by new { card.TenantId, card.LoyaltyProgramId } into g
                select g.Key
            ).CountAsync(ct);

            return count;
        }

        // OPTIMIZATION: Paginated query at database level with all necessary includes
        public async Task<List<LoyaltyCard>> GetLastActiveCardsForApplicationUserPaginatedAsync(
      int applicationUserId,
      string? name,
      int? categoryId,
      int? countryId,
      int? cityId,
      int currentPage,
      int pageSize,
      CancellationToken ct = default)
        {
            var baseQuery = GetAsQueryable()
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    c.Customer != null &&
                    c.Customer.applicationUserId == applicationUserId &&
                    c.Customer.IsActive &&
                    c.Status == CardStatus.Active &&
                    !c.Tenant.IsHidden);

            if (!string.IsNullOrWhiteSpace(name))
                baseQuery = baseQuery.Where(c => EF.Functions.Like(c.Tenant.CompanyName, $"%{name}%"));

            if (categoryId.HasValue)
                baseQuery = baseQuery.Where(c => c.Tenant.categoryId == categoryId.Value);

            if (countryId.HasValue)
                baseQuery = baseQuery.Where(c => 
                    c.Tenant.TenantLocation != null && 
                    c.Tenant.TenantLocation.countryId == countryId.Value);

            if (cityId.HasValue)
                baseQuery = baseQuery.Where(c => 
                    c.Tenant.TenantLocation != null && 
                    c.Tenant.TenantLocation.cityId == cityId.Value);

            // 1) TenantId + LoyaltyProgramId üzrə son kartın Id-sini tap (DB-də)
            var lastIdsQuery =
                from c in baseQuery
                group c by new { c.TenantId, c.LoyaltyProgramId } into g
                select new
                {
                    g.Key.TenantId,
                    g.Key.LoyaltyProgramId,
                    MaxId = g.Max(x => x.Id)
                };

            // 2) Pagination "son kartlar" üzərində olsun
            var pagedLastIds = lastIdsQuery
                .OrderByDescending(x => x.MaxId) // ən son kartlar üstdə
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize);

            // 3) Həmin MaxId-lərə join edib kartları gətir + lazım olan include-lar
            //    (Include-ları minimum saxla!)
            var query =
                from c in baseQuery
                join x in pagedLastIds on c.Id equals x.MaxId
                select c;

            var cards = await query
                .Include(c => c.Customer)
                .Include(c => c.LoyaltyProgram).ThenInclude(p => p.CardTemplate)
                .Include(c => c.Tenant).ThenInclude(t => t.Category)
                .AsSplitQuery()
                .ToListAsync(ct);

            return cards;
        }

        /// <inheritdoc />
        public async Task<(int totalCount, List<LoyaltyCard> cards)> GetMyCardsPaginatedAsync(
            int applicationUserId,
            string? name,
            int? categoryId,
            int? countryId,
            int? cityId,
            int currentPage,
            int pageSize,
            CancellationToken ct = default)
        {
            var baseQuery = GetAsQueryable()
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    c.Customer != null &&
                    c.Customer.applicationUserId == applicationUserId &&
                    c.Customer.IsActive &&
                    c.Status == CardStatus.Active &&
                    !c.Tenant.IsHidden);

            if (!string.IsNullOrWhiteSpace(name))
                baseQuery = baseQuery.Where(c => EF.Functions.Like(c.Tenant.CompanyName, $"%{name}%"));

            if (categoryId.HasValue)
                baseQuery = baseQuery.Where(c => c.Tenant.categoryId == categoryId.Value);

            if (countryId.HasValue)
                baseQuery = baseQuery.Where(c =>
                    c.Tenant.TenantLocation != null &&
                    c.Tenant.TenantLocation.countryId == countryId.Value);

            if (cityId.HasValue)
                baseQuery = baseQuery.Where(c =>
                    c.Tenant.TenantLocation != null &&
                    c.Tenant.TenantLocation.cityId == cityId.Value);

            // Hər (TenantId, LoyaltyProgramId) üçün son kartın Id-si
            var lastIdsQuery =
                from c in baseQuery
                group c by new { c.TenantId, c.LoyaltyProgramId } into g
                select new
                {
                    g.Key.TenantId,
                    g.Key.LoyaltyProgramId,
                    MaxId = g.Max(x => x.Id)
                };

            // Count + Paginated data tək sorğu ilə
            var totalCount = await lastIdsQuery.CountAsync(ct);

            if (totalCount == 0)
                return (0, new List<LoyaltyCard>());

            var pagedLastIds = lastIdsQuery
                .OrderByDescending(x => x.MaxId)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize);

            var cards = await (
                from c in baseQuery
                join x in pagedLastIds on c.Id equals x.MaxId
                select c)
                .Include(c => c.Customer)
                .Include(c => c.LoyaltyProgram).ThenInclude(p => p.CardTemplate)
                .Include(c => c.Tenant).ThenInclude(t => t.Category)
                .AsSplitQuery()
                .ToListAsync(ct);

            return (totalCount, cards);
        }

        /// <inheritdoc />
        public async Task<LoyaltyCard?> GetCardWithFullDetailsAsync(int cardId, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .AsNoTracking()
                .Include(c => c.Customer)
                .Include(c => c.LoyaltyProgram)
                    .ThenInclude(p => p.CardTemplate)
                        .ThenInclude(ct => ct!.Icons)
                .Include(c => c.Tenant)
                    .ThenInclude(t => t.Branding)
                .Include(c => c.Tenant)
                    .ThenInclude(t => t.Category)
                .Where(c => c.Id == cardId)
                .AsSplitQuery()
                .FirstOrDefaultAsync(ct);
        }

        /// <inheritdoc />
        public async Task<LoyaltyCard?> GetCardWithOwnerCheckAsync(int cardId, int applicationUserId, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .AsNoTracking()
                .Include(c => c.Customer)
                .Include(c => c.LoyaltyProgram)
                    .ThenInclude(p => p.CardTemplate)
                        .ThenInclude(ct => ct!.Icons)
                .Include(c => c.Tenant)
                    .ThenInclude(t => t.Branding)
                .Include(c => c.Tenant)
                    .ThenInclude(t => t.Category)
                .Where(c => c.Id == cardId &&
                            c.Customer != null &&
                            c.Customer.applicationUserId == applicationUserId)
                .AsSplitQuery()
                .FirstOrDefaultAsync(ct);
        }

        /// <inheritdoc />
        public async Task<bool> CardBelongsToUserAsync(int cardId, int applicationUserId, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .AsNoTracking()
                .AnyAsync(c =>
                    c.Id == cardId &&
                    c.Customer != null &&
                    c.Customer.applicationUserId == applicationUserId,
                    ct);
        }

    }
}
