using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Bonus qazandırmayan məhsul/qrup/kateqoriya (super admin) + iiko nomenklatura surəti.
    /// Sifariş anında xarici çağırış yoxdur: xəritə DB qaydaları + PosNomenclatureCaches-dən qurulur və
    /// qaydaların imzası ilə yaddaşda keşlənir (qayda/surət dəyişəndə açar dəyişir → dərhal təsir edir).
    /// </summary>
    public class PosEarnExclusionService : IPosEarnExclusionService, IEarnExclusionResolver, IPosNomenclatureJobs, IPosObservedProductRecorder
    {
        private const string IikoSource = "iiko_pos";
        private static readonly TimeSpan MapCacheSliding = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan ObservedCacheTtl = TimeSpan.FromHours(12);

        private readonly LoyaltyWalletDbContext _db;
        private readonly IIikoPosApiClient _iiko;
        private readonly ISensitiveDataProtector _protector;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PosEarnExclusionService> _logger;

        public PosEarnExclusionService(
            LoyaltyWalletDbContext db,
            IIikoPosApiClient iiko,
            ISensitiveDataProtector protector,
            IMemoryCache cache,
            ILogger<PosEarnExclusionService> logger)
        {
            _db = db;
            _iiko = iiko;
            _protector = protector;
            _cache = cache;
            _logger = logger;
        }

        // ───────────────────────── Resolver ─────────────────────────

        public async Task<EarnExclusionMap> GetMapAsync(int tenantIntegrationId, CancellationToken ct = default)
        {
            var rules = await _db.PosEarnExclusions.AsNoTracking()
                .Where(x => x.TenantIntegrationId == tenantIntegrationId && x.IsActive)
                .Select(x => new EarnExclusionRule(x.Kind, x.ExternalId, x.Code, x.Name, x.BlockCashback, x.BlockStamps))
                .ToListAsync(ct);
            if (rules.Count == 0) return EarnExclusionMap.Empty;

            var needsNomenclature = rules.Any(r => r.Kind != PosEarnExclusionKind.Product);
            var stamps = needsNomenclature
                ? await _db.PosNomenclatureCaches.AsNoTracking()
                    .Where(c => c.TenantIntegrationId == tenantIntegrationId)
                    .Select(c => new { c.Id, c.FetchedAtUtc })
                    .ToListAsync(ct)
                : new();

            var signature = new StringBuilder();
            foreach (var r in rules.OrderBy(r => r.Kind).ThenBy(r => r.ExternalId, StringComparer.OrdinalIgnoreCase))
                signature.Append((int)r.Kind).Append(':').Append(r.ExternalId).Append(':').Append(r.Code)
                    .Append(':').Append(r.BlockCashback ? '1' : '0').Append(r.BlockStamps ? '1' : '0').Append('|');
            foreach (var s in stamps.OrderBy(s => s.Id))
                signature.Append('#').Append(s.Id).Append('@').Append(s.FetchedAtUtc.Ticks);
            var key = $"earnx:{tenantIntegrationId}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature.ToString())))}";

            if (_cache.TryGetValue(key, out EarnExclusionMap? cached) && cached != null)
                return cached;

            var snapshot = needsNomenclature ? await LoadSnapshotAsync(tenantIntegrationId, ct) : null;
            var map = EarnExclusionMap.Build(rules, snapshot);
            _cache.Set(key, map, new MemoryCacheEntryOptions { SlidingExpiration = MapCacheSliding });
            return map;
        }

        private async Task<NomenclatureSnapshot?> LoadSnapshotAsync(int tenantIntegrationId, CancellationToken ct)
        {
            var rows = await _db.PosNomenclatureCaches.AsNoTracking()
                .Where(c => c.TenantIntegrationId == tenantIntegrationId)
                .Select(c => new { c.ProductsJson, c.GroupsJson, c.CategoriesJson })
                .ToListAsync(ct);
            if (rows.Count == 0) return null;

            var parts = new List<NomenclatureSnapshot>();
            foreach (var r in rows)
            {
                try
                {
                    parts.Add(new NomenclatureSnapshot
                    {
                        Products = JsonSerializer.Deserialize<List<NomenclatureProduct>>(r.ProductsJson, NomenclatureSnapshot.JsonOptions) ?? new(),
                        Groups = JsonSerializer.Deserialize<List<NomenclatureGroup>>(r.GroupsJson, NomenclatureSnapshot.JsonOptions) ?? new(),
                        Categories = JsonSerializer.Deserialize<List<NomenclatureCategory>>(r.CategoriesJson, NomenclatureSnapshot.JsonOptions) ?? new()
                    });
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Nomenclature cache parse failed. TenantIntegrationId={Id}", tenantIntegrationId);
                }
            }
            return parts.Count == 0 ? null : NomenclatureSnapshot.Merge(parts);
        }

        // ───────────────────────── Sifarişlərdən yığılan məhsullar ─────────────────────────

        public async Task RecordAsync(int tenantIntegrationId, IReadOnlyList<IikoOrderItem> items, CancellationToken ct = default)
        {
            var incoming = new Dictionary<string, (string? Pid, string? Num, string? Name, string? CatId, string? CatName, string Sig)>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var it in items)
            {
                var pid = Clean(it.ProductId, 100);
                var num = Clean(it.ProductNumber, 100);
                var key = pid ?? (num != null ? "code:" + num : null);
                if (key == null) continue;
                var name = Clean(it.ProductName, 300);
                var catId = Clean(it.CategoryId, 100);
                var catName = Clean(it.CategoryName, 200);
                var sig = $"{num}|{name}|{catId}|{catName}";
                if (_cache.TryGetValue($"obs:{tenantIntegrationId}:{key}", out string? cached) && cached == sig) continue;
                incoming[key] = (pid, num, name, catId, catName, sig);
            }
            if (incoming.Count == 0) return;

            var keys = incoming.Keys.ToList();
            var existing = await _db.PosObservedProducts
                .Where(x => x.TenantIntegrationId == tenantIntegrationId && keys.Contains(x.ProductKey))
                .ToListAsync(ct);
            var now = DateTime.UtcNow;

            foreach (var (key, v) in incoming)
            {
                var row = existing.FirstOrDefault(x => string.Equals(x.ProductKey, key, StringComparison.OrdinalIgnoreCase));
                if (row == null)
                {
                    _db.PosObservedProducts.Add(new PosObservedProduct
                    {
                        TenantIntegrationId = tenantIntegrationId,
                        ProductKey = key,
                        ProductId = v.Pid,
                        ProductNumber = v.Num,
                        Name = v.Name,
                        CategoryId = v.CatId,
                        CategoryName = v.CatName,
                        FirstSeenAtUtc = now,
                        LastSeenAtUtc = now,
                        RegUserId = 1,
                        RegDate = now
                    });
                }
                else
                {
                    // Boş gələn sahə köhnə dəyəri silməsin (plugin bəzi event-lərdə kateqoriyanı göndərməyə bilər)
                    row.ProductNumber = v.Num ?? row.ProductNumber;
                    row.Name = v.Name ?? row.Name;
                    row.CategoryId = v.CatId ?? row.CategoryId;
                    row.CategoryName = v.CatName ?? row.CategoryName;
                    row.LastSeenAtUtc = now;
                    row.EditDate = now;
                }
            }

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // Paralel sifariş eyni yeni məhsulu artıq yazıb (unikal indeks) — növbəti sifarişdə yenilənəcək.
                _logger.LogDebug(ex, "Observed products upsert conflict. TenantIntegrationId={Id}", tenantIntegrationId);
                return;
            }

            foreach (var (key, v) in incoming)
                _cache.Set($"obs:{tenantIntegrationId}:{key}", v.Sig, ObservedCacheTtl);
        }

        private static string? Clean(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = s.Trim();
            return t.Length <= max ? t : t[..max];
        }

        private sealed record CatalogProduct(NomenclatureProduct Product, string Source, DateTime? LastSeenAtUtc);

        /// <summary>Cloud nomenklaturası ∪ sifarişlərdən yığılan məhsullar (eyni ID → nomenklatura, boş kateqoriya yığılandan).</summary>
        private sealed class Catalog
        {
            public NomenclatureIndex? Index { get; init; }
            public List<CatalogProduct> Products { get; init; } = new();
            public Dictionary<string, string?> Categories { get; init; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> CategoryCounts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
            public int ObservedCount { get; init; }
            public DateTime? ObservedLastSeenAtUtc { get; init; }
        }

        private async Task<Catalog> LoadCatalogAsync(int tenantIntegrationId, CancellationToken ct)
        {
            var index = NomenclatureIndex.From(await LoadSnapshotAsync(tenantIntegrationId, ct));
            var observed = await _db.PosObservedProducts.AsNoTracking()
                .Where(x => x.TenantIntegrationId == tenantIntegrationId)
                .Select(x => new { x.ProductKey, x.ProductId, x.ProductNumber, x.Name, x.CategoryId, x.CategoryName, x.LastSeenAtUtc })
                .ToListAsync(ct);

            var cmp = StringComparer.OrdinalIgnoreCase;
            var products = new Dictionary<string, CatalogProduct>(cmp);
            var categories = new Dictionary<string, string?>(cmp);
            if (index != null)
            {
                foreach (var p in index.Snapshot.Products) products.TryAdd(p.Id, new CatalogProduct(p, "iiko", null));
                foreach (var c in index.Snapshot.Categories) categories.TryAdd(c.Id, c.Name);
            }
            foreach (var o in observed)
            {
                var id = o.ProductId ?? o.ProductKey;
                if (o.CategoryId != null && (!categories.TryGetValue(o.CategoryId, out var n) || n == null))
                    categories[o.CategoryId] = o.CategoryName;
                if (products.TryGetValue(id, out var known))
                {
                    var p = known.Product;
                    products[id] = known with
                    {
                        Product = p with { Code = p.Code ?? o.ProductNumber, CategoryId = p.CategoryId ?? o.CategoryId },
                        LastSeenAtUtc = o.LastSeenAtUtc
                    };
                }
                else
                {
                    products[id] = new CatalogProduct(
                        new NomenclatureProduct(id, o.ProductNumber, o.Name, null, o.CategoryId, null), "orders", o.LastSeenAtUtc);
                }
            }

            var counts = products.Values
                .Where(p => p.Product.CategoryId != null)
                .GroupBy(p => p.Product.CategoryId!, cmp)
                .ToDictionary(g => g.Key, g => g.Count(), cmp);

            return new Catalog
            {
                Index = index,
                Products = products.Values.ToList(),
                Categories = categories,
                CategoryCounts = counts,
                ObservedCount = observed.Count,
                ObservedLastSeenAtUtc = observed.Count == 0 ? null : observed.Max(o => o.LastSeenAtUtc)
            };
        }

        // ───────────────────────── CRUD ─────────────────────────

        public async Task<ServiceResult<List<PosEarnExclusionDto>>> ListAsync(int tenantIntegrationId, CancellationToken ct = default)
        {
            if (!await _db.TenantIntegrations.AnyAsync(t => t.Id == tenantIntegrationId, ct))
                return ServiceResult<List<PosEarnExclusionDto>>.Fail("İnteqrasiya tapılmadı.");

            var rows = await _db.PosEarnExclusions.AsNoTracking()
                .Where(x => x.TenantIntegrationId == tenantIntegrationId)
                .OrderBy(x => x.Kind).ThenBy(x => x.Name)
                .ToListAsync(ct);

            var catalog = rows.Any(r => r.Kind != PosEarnExclusionKind.Product)
                ? await LoadCatalogAsync(tenantIntegrationId, ct)
                : null;
            var index = catalog?.Index;

            var list = rows.Select(r => new PosEarnExclusionDto
            {
                Id = r.Id,
                TenantIntegrationId = r.TenantIntegrationId,
                Kind = r.Kind,
                ExternalId = r.ExternalId,
                Code = r.Code,
                Name = r.Name,
                BlockCashback = r.BlockCashback,
                BlockStamps = r.BlockStamps,
                Note = r.Note,
                IsActive = r.IsActive,
                RegDate = r.RegDate,
                ProductCount = r.Kind switch
                {
                    PosEarnExclusionKind.Group => index?.ProductCountInGroup(r.ExternalId),
                    PosEarnExclusionKind.Category => catalog?.CategoryCounts.GetValueOrDefault(r.ExternalId),
                    _ => null
                },
                Path = r.Kind == PosEarnExclusionKind.Group ? index?.GroupPath(r.ExternalId) : null
            }).ToList();

            return ServiceResult<List<PosEarnExclusionDto>>.Ok(list);
        }

        public async Task<ServiceResult<List<PosEarnExclusionDto>>> UpsertAsync(
            int tenantIntegrationId, List<PosEarnExclusionCreateDto> items, int userId, CancellationToken ct = default)
        {
            if (items == null || items.Count == 0)
                return ServiceResult<List<PosEarnExclusionDto>>.Fail("Siyahı boşdur.");
            if (!await _db.TenantIntegrations.AnyAsync(t => t.Id == tenantIntegrationId, ct))
                return ServiceResult<List<PosEarnExclusionDto>>.Fail("İnteqrasiya tapılmadı.");

            foreach (var it in items)
            {
                if (!Enum.IsDefined(typeof(PosEarnExclusionKind), it.Kind))
                    return ServiceResult<List<PosEarnExclusionDto>>.Fail("Növ yanlışdır.");
                if (string.IsNullOrWhiteSpace(it.ExternalId) || it.ExternalId.Trim().Length > 100)
                    return ServiceResult<List<PosEarnExclusionDto>>.Fail("ID tələb olunur (≤ 100 simvol).");
                // Kateqoriya ən ümumi səviyyədir — "heç nəyi bloklamayan" kateqoriya qaydası mənasızdır.
                if (it.Kind == PosEarnExclusionKind.Category && !it.BlockCashback && !it.BlockStamps)
                    return ServiceResult<List<PosEarnExclusionDto>>.Fail("Kateqoriya üçün ən azı bir bonus növü seçilməlidir.");
            }

            var index = NomenclatureIndex.From(await LoadSnapshotAsync(tenantIntegrationId, ct));
            var existing = await _db.PosEarnExclusions
                .Where(x => x.TenantIntegrationId == tenantIntegrationId)
                .ToListAsync(ct);
            var now = DateTime.UtcNow;

            foreach (var it in items)
            {
                var extId = it.ExternalId.Trim();
                var name = string.IsNullOrWhiteSpace(it.Name) ? index?.NameOf(it.Kind, extId) : it.Name.Trim();
                var code = string.IsNullOrWhiteSpace(it.Code)
                    ? (it.Kind == PosEarnExclusionKind.Product ? index?.CodeOf(extId) : null)
                    : it.Code.Trim();

                var row = existing.FirstOrDefault(x => x.Kind == it.Kind && string.Equals(x.ExternalId, extId, StringComparison.OrdinalIgnoreCase));
                if (row == null)
                {
                    row = new PosEarnExclusion
                    {
                        TenantIntegrationId = tenantIntegrationId,
                        Kind = it.Kind,
                        ExternalId = extId,
                        RegUserId = userId,
                        RegDate = now
                    };
                    _db.PosEarnExclusions.Add(row);
                    existing.Add(row);
                }
                else
                {
                    row.EditUserId = userId;
                    row.EditDate = now;
                }
                row.Name = Truncate(name, 300);
                row.Code = Truncate(code, 100);
                row.BlockCashback = it.BlockCashback;
                row.BlockStamps = it.BlockStamps;
                row.Note = Truncate(string.IsNullOrWhiteSpace(it.Note) ? null : it.Note.Trim(), 500);
                row.IsActive = true;
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Earn exclusions upserted. TenantIntegrationId={Id} Count={Count} UserId={UserId}",
                tenantIntegrationId, items.Count, userId);
            return await ListAsync(tenantIntegrationId, ct);
        }

        public async Task<ServiceResult<bool>> UpdateAsync(int tenantIntegrationId, int id, PosEarnExclusionUpdateDto dto, int userId, CancellationToken ct = default)
        {
            var row = await _db.PosEarnExclusions.FirstOrDefaultAsync(x => x.Id == id && x.TenantIntegrationId == tenantIntegrationId, ct);
            if (row == null) return ServiceResult<bool>.Fail("Tapılmadı.");

            var cashback = dto.BlockCashback ?? row.BlockCashback;
            var stamps = dto.BlockStamps ?? row.BlockStamps;
            if (row.Kind == PosEarnExclusionKind.Category && !cashback && !stamps)
                return ServiceResult<bool>.Fail("Kateqoriya üçün ən azı bir bonus növü seçilməlidir.");

            row.BlockCashback = cashback;
            row.BlockStamps = stamps;
            if (dto.Note != null) row.Note = Truncate(string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(), 500);
            if (dto.IsActive.HasValue) row.IsActive = dto.IsActive.Value;
            row.EditUserId = userId;
            row.EditDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(int tenantIntegrationId, int id, CancellationToken ct = default)
        {
            var row = await _db.PosEarnExclusions.FirstOrDefaultAsync(x => x.Id == id && x.TenantIntegrationId == tenantIntegrationId, ct);
            if (row == null) return ServiceResult<bool>.Fail("Tapılmadı.");
            _db.PosEarnExclusions.Remove(row);
            await _db.SaveChangesAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }

        // ───────────────────────── Nomenklatura ─────────────────────────

        public async Task<ServiceResult<PosNomenclatureDto>> GetNomenclatureAsync(int tenantIntegrationId, CancellationToken ct = default)
        {
            var meta = await _db.PosNomenclatureCaches.AsNoTracking()
                .Where(c => c.TenantIntegrationId == tenantIntegrationId)
                .Select(c => c.FetchedAtUtc)
                .ToListAsync(ct);
            var catalog = await LoadCatalogAsync(tenantIntegrationId, ct);
            var index = catalog.Index;

            return ServiceResult<PosNomenclatureDto>.Ok(new PosNomenclatureDto
            {
                FetchedAtUtc = meta.Count == 0 ? null : meta.Min(),
                OrganizationCount = meta.Count,
                ProductCount = index?.Snapshot.Products.Count ?? 0,
                ObservedProductCount = catalog.ObservedCount,
                ObservedLastSeenAtUtc = catalog.ObservedLastSeenAtUtc,
                Groups = index == null
                    ? new()
                    : index.Snapshot.Groups
                        .Select(g => new PosNomenclatureGroupDto
                        {
                            Id = g.Id,
                            Name = g.Name,
                            ParentGroup = g.ParentGroup != null && index.HasGroup(g.ParentGroup) ? g.ParentGroup : null,
                            ProductCount = index.ProductCountInGroup(g.Id)
                        })
                        .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList(),
                Categories = catalog.Categories
                    .Select(c => new PosNomenclatureCategoryDto
                    {
                        Id = c.Key,
                        Name = c.Value,
                        ProductCount = catalog.CategoryCounts.GetValueOrDefault(c.Key)
                    })
                    .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
            });
        }

        public async Task<ServiceResult<List<PosNomenclatureProductDto>>> SearchProductsAsync(
            int tenantIntegrationId, string? q, string? groupId, string? categoryId, int take, CancellationToken ct = default)
        {
            var catalog = await LoadCatalogAsync(tenantIntegrationId, ct);
            var index = catalog.Index;

            take = Math.Clamp(take <= 0 ? 50 : take, 1, 300);
            IEnumerable<CatalogProduct> query = catalog.Products;

            if (!string.IsNullOrWhiteSpace(groupId))
            {
                // Qrup yalnız Cloud nomenklaturasında var — sifarişdən yığılan məhsullar qrupsuzdur
                var groups = index?.DescendantGroups(groupId.Trim()) ?? new HashSet<string>();
                query = query.Where(p => p.Product.ParentGroup != null && groups.Contains(p.Product.ParentGroup));
            }
            if (!string.IsNullOrWhiteSpace(categoryId))
                query = query.Where(p => string.Equals(p.Product.CategoryId, categoryId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(p =>
                    (p.Product.Name != null && p.Product.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)) ||
                    string.Equals(p.Product.Code, term, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Product.Id, term, StringComparison.OrdinalIgnoreCase));
            }

            var map = await GetMapAsync(tenantIntegrationId, ct);
            var list = query
                .OrderBy(p => p.Product.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(take)
                .Select(cp =>
                {
                    var p = cp.Product;
                    return new PosNomenclatureProductDto
                    {
                        Id = p.Id.StartsWith("code:", StringComparison.Ordinal) ? p.Code ?? p.Id : p.Id,
                        Code = p.Code,
                        Name = p.Name,
                        Type = p.Type,
                        GroupId = p.ParentGroup,
                        GroupName = p.ParentGroup == null ? null : index?.GroupPath(p.ParentGroup),
                        CategoryId = p.CategoryId,
                        CategoryName = p.CategoryId == null ? null : catalog.Categories.GetValueOrDefault(p.CategoryId),
                        Source = cp.Source,
                        LastSeenAtUtc = cp.LastSeenAtUtc,
                        Effective = EarnBlockDto.From(map.Lookup(p.Id, p.Code, p.CategoryId))
                    };
                })
                .ToList();
            return ServiceResult<List<PosNomenclatureProductDto>>.Ok(list);
        }

        public async Task<ServiceResult<PosNomenclatureRefreshResultDto>> RefreshNomenclatureAsync(int tenantIntegrationId, CancellationToken ct = default)
        {
            var ti = await _db.TenantIntegrations
                .Include(t => t.IntegrationType)
                .FirstOrDefaultAsync(t => t.Id == tenantIntegrationId, ct);
            if (ti == null) return ServiceResult<PosNomenclatureRefreshResultDto>.Fail("İnteqrasiya tapılmadı.");
            if (ti.IntegrationType?.Source != IikoSource)
                return ServiceResult<PosNomenclatureRefreshResultDto>.Fail("Nomenklatura yalnız iiko inteqrasiyası üçündür.");
            if (string.IsNullOrEmpty(ti.ApiTokenEncrypted))
                return ServiceResult<PosNomenclatureRefreshResultDto>.Fail("apiLogin konfiqurasiya edilməyib.");

            var apiLogin = _protector.Decrypt(ti.ApiTokenEncrypted);
            if (string.IsNullOrEmpty(apiLogin))
                return ServiceResult<PosNomenclatureRefreshResultDto>.Fail("apiLogin oxunmadı.");
            var clientSecret = string.IsNullOrEmpty(ti.ApiSecretEncrypted) ? null : _protector.Decrypt(ti.ApiSecretEncrypted);

            var token = await _iiko.GetAccessTokenAsync(apiLogin, ti.ApiAppId, clientSecret, ct: ct);
            if (string.IsNullOrEmpty(token))
                return ServiceResult<PosNomenclatureRefreshResultDto>.Fail("iiko access_token alınmadı (credential-ları yoxla).");

            // Org-lar: inteqrasiyanın öz org-u + aktiv venue-lar; heç biri yoxdursa apiLogin-in bütün org-ları.
            var orgIds = new HashSet<Guid>();
            if (Guid.TryParse(ti.ExternalVenueId, out var mainOrg)) orgIds.Add(mainOrg);
            var venueOrgs = await _db.TenantIntegrationVenues.AsNoTracking()
                .Where(v => v.TenantIntegrationId == tenantIntegrationId && v.IsActive)
                .Select(v => v.ExternalVenueId)
                .ToListAsync(ct);
            foreach (var v in venueOrgs)
                if (Guid.TryParse(v, out var g)) orgIds.Add(g);
            if (orgIds.Count == 0)
            {
                var orgs = await _iiko.GetOrganizationsAsync(token, ct);
                foreach (var o in orgs ?? Array.Empty<IikoOrganization>()) orgIds.Add(o.Id);
            }
            if (orgIds.Count == 0)
                return ServiceResult<PosNomenclatureRefreshResultDto>.Fail("iiko təşkilatı tapılmadı.");

            var existing = await _db.PosNomenclatureCaches
                .Where(c => c.TenantIntegrationId == tenantIntegrationId)
                .ToListAsync(ct);
            var now = DateTime.UtcNow;
            var parts = new List<NomenclatureSnapshot>();
            int failed = 0;

            foreach (var orgId in orgIds)
            {
                IikoNomenclatureResponse? nom = null;
                try { nom = await _iiko.GetNomenclatureAsync(token, orgId, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "iiko nomenclature failed. OrgId={OrgId}", orgId); }
                if (nom == null) { failed++; continue; }

                var snap = ToSnapshot(nom);
                parts.Add(snap);

                var key = orgId.ToString();
                var row = existing.FirstOrDefault(c => string.Equals(c.OrganizationId, key, StringComparison.OrdinalIgnoreCase));
                if (row == null)
                {
                    row = new PosNomenclatureCache
                    {
                        TenantIntegrationId = tenantIntegrationId,
                        OrganizationId = key,
                        RegUserId = 1,
                        RegDate = now
                    };
                    _db.PosNomenclatureCaches.Add(row);
                }
                else
                {
                    row.EditDate = now;
                }
                row.ProductsJson = JsonSerializer.Serialize(snap.Products, NomenclatureSnapshot.JsonOptions);
                row.GroupsJson = JsonSerializer.Serialize(snap.Groups, NomenclatureSnapshot.JsonOptions);
                row.CategoriesJson = JsonSerializer.Serialize(snap.Categories, NomenclatureSnapshot.JsonOptions);
                row.Revision = nom.Revision;
                row.FetchedAtUtc = now;
            }

            if (parts.Count == 0)
                return ServiceResult<PosNomenclatureRefreshResultDto>.Fail("iiko nomenklaturası alınmadı.");

            await _db.SaveChangesAsync(ct);

            var merged = NomenclatureSnapshot.Merge(parts);
            _logger.LogInformation(
                "iiko nomenclature refreshed. TenantIntegrationId={Id} Orgs={Orgs} Failed={Failed} Products={Products} Groups={Groups} Categories={Categories}",
                tenantIntegrationId, parts.Count, failed, merged.Products.Count, merged.Groups.Count, merged.Categories.Count);

            return ServiceResult<PosNomenclatureRefreshResultDto>.Ok(new PosNomenclatureRefreshResultDto
            {
                Organizations = parts.Count,
                FailedOrganizations = failed,
                Products = merged.Products.Count,
                Groups = merged.Groups.Count,
                Categories = merged.Categories.Count,
                FetchedAtUtc = now
            });
        }

        internal static NomenclatureSnapshot ToSnapshot(IikoNomenclatureResponse nom) => new()
        {
            Products = (nom.Products ?? new())
                .Where(p => !p.IsDeleted)
                .Select(p => new NomenclatureProduct(
                    p.Id.ToString(),
                    string.IsNullOrWhiteSpace(p.Code) ? null : p.Code.Trim(),
                    p.Name,
                    (p.ParentGroup ?? p.GroupId)?.ToString(),
                    p.ProductCategoryId?.ToString(),
                    p.Type))
                .ToList(),
            Groups = (nom.Groups ?? new())
                .Where(g => !g.IsDeleted)
                .Select(g => new NomenclatureGroup(g.Id.ToString(), g.Name, g.ParentGroup?.ToString()))
                .ToList(),
            Categories = (nom.ProductCategories ?? new())
                .Where(c => !c.IsDeleted)
                .Select(c => new NomenclatureCategory(c.Id.ToString(), c.Name))
                .ToList()
        };

        [AutomaticRetry(Attempts = 0)]
        public async Task RefreshAllAsync(CancellationToken ct = default)
        {
            var ids = await _db.PosEarnExclusions.AsNoTracking()
                .Where(x => x.IsActive && x.Kind != PosEarnExclusionKind.Product)
                .Select(x => x.TenantIntegrationId)
                .Distinct()
                .ToListAsync(ct);

            foreach (var id in ids)
            {
                try
                {
                    var r = await RefreshNomenclatureAsync(id, ct);
                    if (!r.Success)
                        _logger.LogWarning("iiko nomenclature refresh skipped. TenantIntegrationId={Id} Reason={Reason}", id, r.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "iiko nomenclature refresh failed. TenantIntegrationId={Id}", id);
                }
            }
        }

        // ───────────────────────── Simulyasiya ─────────────────────────

        public async Task<ServiceResult<EarnExclusionSimulateResultDto>> SimulateAsync(
            int tenantIntegrationId, EarnExclusionSimulateRequestDto request, CancellationToken ct = default)
        {
            if (request?.Items == null || request.Items.Count == 0)
                return ServiceResult<EarnExclusionSimulateResultDto>.Fail("Məhsul siyahısı boşdur.");

            var map = await GetMapAsync(tenantIntegrationId, ct);
            var rules = await _db.PosProductEarnRules.AsNoTracking()
                .Where(r => r.TenantIntegrationId == tenantIntegrationId && r.IsActive)
                .ToListAsync(ct);
            var rulesByProduct = rules
                .GroupBy(r => r.ExternalProductId.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            EarnRuleHit? RuleFor(EarnLine l)
            {
                PosProductEarnRule? r = null;
                var pid = (l.ProductId ?? "").Trim();
                var pnum = (l.ProductNumber ?? "").Trim();
                if (pid.Length > 0) rulesByProduct.TryGetValue(pid, out r);
                if (r == null && pnum.Length > 0) rulesByProduct.TryGetValue(pnum, out r);
                return r == null ? null : new EarnRuleHit(r.StepsPerUnit, r.PointsPerUnit);
            }

            var lines = request.Items
                .Select(i => new EarnLine(i.ProductId, i.ProductNumber, i.CategoryId, i.Name, i.Amount, i.Sum))
                .ToList();
            var discounts = request.Items
                .Where(i => i.DiscountSum != 0m && !string.IsNullOrWhiteSpace(i.ProductId))
                .Select(i => new EarnItemDiscount(i.ProductId, i.DiscountSum))
                .ToList();
            var total = request.TotalSum ?? request.Items.Sum(i => i.Sum - i.DiscountSum);
            var wallet = Math.Max(0m, request.WalletSum);

            var baseResult = EarnExclusionMath.Compute(lines, discounts, total, wallet, request.IsProgress, map);
            var stamp = EarnExclusionMath.ApplyEarnRules(lines, RuleFor, _ => false, map);
            var legacyBase = request.IsProgress ? total : Math.Max(0m, total - wallet);

            var result = new EarnExclusionSimulateResultDto
            {
                TotalSum = total,
                EarnBase = legacyBase,
                CashbackBase = baseResult.CashbackBase,
                ExcludedNet = baseResult.ExcludedNet,
                Steps = stamp.Steps,
                ProductPoints = stamp.Points,
                AnyEarnRuleMatched = stamp.AnyRuleMatched,
                StampSkippedUnits = stamp.SkippedUnits
            };
            if (request.CashbackPercent is > 0m)
            {
                result.ExpectedCashback = Math.Round(baseResult.CashbackBase * request.CashbackPercent.Value / 100m, 2);
                result.ExpectedCashbackWithoutExclusions = Math.Round(legacyBase * request.CashbackPercent.Value / 100m, 2);
            }

            int blockedIdx = 0;
            foreach (var l in lines)
            {
                var block = map.Blocked(l.ProductId, l.ProductNumber, l.CategoryId);
                decimal excluded = 0m;
                if (block != null && blockedIdx < baseResult.BlockedLines.Count)
                {
                    var bl = baseResult.BlockedLines[blockedIdx++];
                    if (block.BlockCashback) excluded = bl.Net;
                }
                var rule = RuleFor(l);
                result.Lines.Add(new EarnExclusionSimulateLineDto
                {
                    ProductId = l.ProductId,
                    Name = l.Name,
                    Amount = l.Amount,
                    Sum = l.Sum,
                    CashbackBlocked = block?.BlockCashback == true,
                    StampsBlocked = block?.BlockStamps == true,
                    Reason = block == null ? null : $"{KindLabel(block.SourceKind)}: {block.SourceName}",
                    ExcludedNet = excluded,
                    StepsPerUnit = rule?.StepsPerUnit,
                    PointsPerUnit = rule?.PointsPerUnit
                });
            }

            return ServiceResult<EarnExclusionSimulateResultDto>.Ok(result);
        }

        internal static string KindLabel(PosEarnExclusionKind kind) => kind switch
        {
            PosEarnExclusionKind.Group => "Qrup",
            PosEarnExclusionKind.Category => "Kateqoriya",
            _ => "Məhsul"
        };

        private static string? Truncate(string? s, int max) => s == null ? null : (s.Length <= max ? s : s[..max]);

        /// <summary>Nomenklatura surəti üzərində qrup ağacı / say / ad sorğuları.</summary>
        private sealed class NomenclatureIndex
        {
            public NomenclatureSnapshot Snapshot { get; }
            private readonly Dictionary<string, NomenclatureGroup> _groups;
            private readonly Dictionary<string, List<string>> _children;
            private readonly Dictionary<string, int> _directProducts;
            private readonly Dictionary<string, int> _categoryProducts;
            private readonly Dictionary<string, NomenclatureProduct> _products;
            private readonly Dictionary<string, string?> _categoryNames;
            private readonly Dictionary<string, int> _recursiveCount = new(StringComparer.OrdinalIgnoreCase);

            private NomenclatureIndex(NomenclatureSnapshot s)
            {
                var cmp = StringComparer.OrdinalIgnoreCase;
                Snapshot = s;
                _groups = s.Groups.GroupBy(g => g.Id, cmp).ToDictionary(g => g.Key, g => g.First(), cmp);
                _children = s.Groups.Where(g => g.ParentGroup != null)
                    .GroupBy(g => g.ParentGroup!, cmp)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList(), cmp);
                _directProducts = s.Products.Where(p => p.ParentGroup != null)
                    .GroupBy(p => p.ParentGroup!, cmp)
                    .ToDictionary(g => g.Key, g => g.Count(), cmp);
                _categoryProducts = s.Products.Where(p => p.CategoryId != null)
                    .GroupBy(p => p.CategoryId!, cmp)
                    .ToDictionary(g => g.Key, g => g.Count(), cmp);
                _products = s.Products.GroupBy(p => p.Id, cmp).ToDictionary(g => g.Key, g => g.First(), cmp);
                _categoryNames = s.Categories.GroupBy(c => c.Id, cmp).ToDictionary(g => g.Key, g => g.First().Name, cmp);
            }

            public static NomenclatureIndex? From(NomenclatureSnapshot? s) => s == null ? null : new NomenclatureIndex(s);

            public bool HasGroup(string id) => _groups.ContainsKey(id);

            public HashSet<string> DescendantGroups(string groupId)
            {
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var stack = new Stack<string>();
                stack.Push(groupId);
                while (stack.Count > 0)
                {
                    var g = stack.Pop();
                    if (!result.Add(g)) continue;
                    if (_children.TryGetValue(g, out var kids))
                        foreach (var k in kids) stack.Push(k);
                }
                return result;
            }

            public int ProductCountInGroup(string groupId)
            {
                if (_recursiveCount.TryGetValue(groupId, out var cached)) return cached;
                var count = DescendantGroups(groupId).Sum(g => _directProducts.GetValueOrDefault(g));
                _recursiveCount[groupId] = count;
                return count;
            }

            public int ProductCountInCategory(string categoryId) => _categoryProducts.GetValueOrDefault(categoryId);

            public string? GroupPath(string groupId)
            {
                var names = new List<string>();
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string? g = groupId;
                while (g != null && visited.Add(g) && _groups.TryGetValue(g, out var grp))
                {
                    names.Add(grp.Name ?? "?");
                    g = grp.ParentGroup;
                }
                if (names.Count == 0) return null;
                names.Reverse();
                return string.Join(" / ", names);
            }

            public string? NameOf(PosEarnExclusionKind kind, string id) => kind switch
            {
                PosEarnExclusionKind.Group => _groups.TryGetValue(id, out var g) ? g.Name : null,
                PosEarnExclusionKind.Category => _categoryNames.TryGetValue(id, out var n) ? n : null,
                _ => _products.TryGetValue(id, out var p) ? p.Name : null
            };

            public string? CodeOf(string productId) => _products.TryGetValue(productId, out var p) ? p.Code : null;
        }
    }
}
