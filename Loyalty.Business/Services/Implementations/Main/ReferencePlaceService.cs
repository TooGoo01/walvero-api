using Loyalty.Business.Dtos.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class ReferencePlaceService : IReferencePlaceService
    {
        private readonly LoyaltyWalletDbContext _db;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "ai:reference-places";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public ReferencePlaceService(LoyaltyWalletDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<ServiceResult> GetAllAsync(CancellationToken ct = default)
        {
            var list = await _db.AiReferencePlaces
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => Map(p))
                .ToListAsync(ct);
            return new ServiceResult(true, list);
        }

        public async Task<ServiceResult> AddAsync(CreateAiReferencePlaceDto dto, CancellationToken ct = default)
        {
            var menuJsonError = ValidateMenuJson(dto.MenuJson);
            if (menuJsonError != null) return new ServiceResult(false, menuJsonError);

            var e = new AiReferencePlace();
            Apply(e, dto);
            e.RegDate = DateTime.UtcNow;
            e.IsActive = dto.IsActive;
            _db.AiReferencePlaces.Add(e);
            await _db.SaveChangesAsync(ct);
            _cache.Remove(CacheKey);
            return new ServiceResult(true, Map(e));
        }

        public async Task<ServiceResult> UpdateAsync(UpdateAiReferencePlaceDto dto, CancellationToken ct = default)
        {
            var menuJsonError = ValidateMenuJson(dto.MenuJson);
            if (menuJsonError != null) return new ServiceResult(false, menuJsonError);

            var e = await _db.AiReferencePlaces.FirstOrDefaultAsync(p => p.Id == dto.Id, ct);
            if (e == null) return new ServiceResult(false, "Məkan tapılmadı");
            Apply(e, dto);
            e.IsActive = dto.IsActive;
            e.EditDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _cache.Remove(CacheKey);
            return new ServiceResult(true, Map(e));
        }

        public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
        {
            var e = await _db.AiReferencePlaces.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (e == null) return new ServiceResult(false, "Məkan tapılmadı");
            _db.AiReferencePlaces.Remove(e);
            await _db.SaveChangesAsync(ct);
            _cache.Remove(CacheKey);
            return new ServiceResult(true, "Silindi");
        }

        public async Task<List<GetAiReferencePlaceDto>> SearchAsync(string[] keywords, string? area, int take = 8, CancellationToken ct = default)
        {
            var all = await GetActiveCachedAsync(ct);

            var tokens = new List<string>();
            if (keywords != null) tokens.AddRange(keywords);
            if (!string.IsNullOrWhiteSpace(area)) tokens.Add(area!);
            var normTokens = tokens
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(Normalize)
                .Where(t => t.Length > 0)
                .Distinct()
                .ToList();

            if (normTokens.Count == 0)
                return new List<GetAiReferencePlaceDto>();

            var scored = new List<(AiReferencePlace p, int score)>();
            foreach (var p in all)
            {
                // MenuJson raw string də haystack-dədir — yemək adları ilə xarici məkan tapılsın
                // (mötərizə/dırnaq Normalize-dan sonra zərərsiz "noise"-dur).
                var haystack = Normalize(string.Join(" ", new[]
                {
                    p.Name, p.Category, p.City, p.Area, p.Address, p.Keywords, p.Description, p.MenuJson
                }.Where(s => !string.IsNullOrWhiteSpace(s))));

                var score = normTokens.Count(tok => haystack.Contains(tok, StringComparison.Ordinal));
                if (score > 0) scored.Add((p, score));
            }

            return scored
                .OrderByDescending(x => x.score)
                .ThenBy(x => x.p.Name)
                .Take(Math.Clamp(take <= 0 ? 8 : take, 1, 20))
                .Select(x => Map(x.p))
                .ToList();
        }

        public async Task<GetAiReferencePlaceDto?> FindByNameAsync(string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var normQuery = Normalize(name);
            if (normQuery.Length == 0) return null;

            var all = await GetActiveCachedAsync(ct);

            // Ən yaxşı uyğunluq: ad ↔ sorğu qarşılıqlı contains; ən qısa ad (ən spesifik) üstün.
            var match = all
                .Select(p => new { p, norm = Normalize(p.Name) })
                .Where(x => x.norm.Length > 0 &&
                            (x.norm.Contains(normQuery, StringComparison.Ordinal) ||
                             normQuery.Contains(x.norm, StringComparison.Ordinal)))
                .OrderBy(x => x.norm.Length)
                .FirstOrDefault();

            return match == null ? null : Map(match.p);
        }

        private async Task<List<AiReferencePlace>> GetActiveCachedAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(CacheKey, out List<AiReferencePlace>? cached) && cached is not null)
                return cached;
            var list = await _db.AiReferencePlaces.AsNoTracking().Where(p => p.IsActive).ToListAsync(ct);
            _cache.Set(CacheKey, list, CacheTtl);
            return list;
        }

        private static void Apply(AiReferencePlace e, CreateAiReferencePlaceDto d)
        {
            e.Name = d.Name.Trim();
            e.Category = d.Category?.Trim();
            e.City = d.City?.Trim();
            e.Area = d.Area?.Trim();
            e.Address = d.Address?.Trim();
            e.Keywords = d.Keywords?.Trim();
            e.Description = d.Description?.Trim();
            e.Phone = d.Phone?.Trim();
            e.MenuUrl = d.MenuUrl?.Trim();
            e.MenuJson = string.IsNullOrWhiteSpace(d.MenuJson) ? null : d.MenuJson.Trim();
        }

        /// <summary>MenuJson doludursa valid JSON olmalıdır; xəta mesajı və ya null qaytarır.</summary>
        private static string? ValidateMenuJson(string? menuJson)
        {
            if (string.IsNullOrWhiteSpace(menuJson)) return null;
            try
            {
                using var _ = System.Text.Json.JsonDocument.Parse(menuJson);
                return null;
            }
            catch
            {
                return "Menyu JSON formatı yanlışdır";
            }
        }

        private static GetAiReferencePlaceDto Map(AiReferencePlace p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Category = p.Category,
            City = p.City,
            Area = p.Area,
            Address = p.Address,
            Keywords = p.Keywords,
            Description = p.Description,
            Phone = p.Phone,
            MenuUrl = p.MenuUrl,
            MenuJson = p.MenuJson,
            IsActive = p.IsActive,
        };

        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.ToLowerInvariant().Trim();
            var map = new Dictionary<char, char>
            {
                ['ə'] = 'e', ['ğ'] = 'g', ['ı'] = 'i', ['ö'] = 'o', ['ü'] = 'u',
                ['ş'] = 's', ['ç'] = 'c'
            };
            var chars = s.Select(c => map.TryGetValue(c, out var r) ? r : c).ToArray();
            return new string(chars);
        }
    }
}
