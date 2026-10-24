using System.Text.RegularExpressions;
using Loyalty.Business.Dtos.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class LinkPageService : ILinkPageService
    {
        private static readonly Regex SlugRx = new(@"^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$", RegexOptions.Compiled);
        private static readonly Regex HexRx = new(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
        private static readonly TimeSpan PublicCacheTtl = TimeSpan.FromSeconds(60);

        private readonly LoyaltyWalletDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly ILogger<LinkPageService> _logger;

        public LinkPageService(LoyaltyWalletDbContext db, IMemoryCache cache, ILogger<LinkPageService> logger)
        {
            _db = db;
            _cache = cache;
            _logger = logger;
        }

        private static string PublicKey(string slug) => $"linkpage:public:{slug}";

        // ───────────────────────── Admin ─────────────────────────

        public async Task<ServiceResult<List<GetLinkPageDto>>> ListAsync(CancellationToken ct = default)
        {
            var pages = await _db.LinkPages.AsNoTracking()
                .Include(p => p.Items)
                .OrderByDescending(p => p.RegDate)
                .ToListAsync(ct);
            var clicks = await ClickCountsAsync(pages.Select(p => p.Id).ToList(), ct);
            return ServiceResult<List<GetLinkPageDto>>.Ok(pages.Select(p => Map(p, clicks, includeInactive: true)).ToList());
        }

        public async Task<ServiceResult<GetLinkPageDto>> GetAsync(int id, CancellationToken ct = default)
        {
            var page = await _db.LinkPages.AsNoTracking().Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, ct);
            if (page == null) return ServiceResult<GetLinkPageDto>.Fail("Səhifə tapılmadı.");
            var clicks = await ClickCountsAsync(new List<int> { id }, ct);
            return ServiceResult<GetLinkPageDto>.Ok(Map(page, clicks, includeInactive: true));
        }

        public async Task<ServiceResult<GetLinkPageDto>> CreateAsync(CreateLinkPageDto dto, CancellationToken ct = default)
        {
            var error = await ValidateAsync(dto, null, ct);
            if (error != null) return ServiceResult<GetLinkPageDto>.Fail(error);

            var page = new LinkPage { RegDate = DateTime.UtcNow };
            Apply(page, dto);
            page.Items = BuildItems(dto.Items, page);
            _db.LinkPages.Add(page);
            await _db.SaveChangesAsync(ct);
            _cache.Remove(PublicKey(page.Slug));
            return await GetAsync(page.Id, ct);
        }

        public async Task<ServiceResult<GetLinkPageDto>> UpdateAsync(UpdateLinkPageDto dto, CancellationToken ct = default)
        {
            var page = await _db.LinkPages.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == dto.Id, ct);
            if (page == null) return ServiceResult<GetLinkPageDto>.Fail("Səhifə tapılmadı.");

            var error = await ValidateAsync(dto, page.Id, ct);
            if (error != null) return ServiceResult<GetLinkPageDto>.Fail(error);

            var oldSlug = page.Slug;
            Apply(page, dto);
            page.EditDate = DateTime.UtcNow;

            // Elementlər: Id-si gələnlər yenilənir (klik tarixçəsi qalır), gəlməyənlər silinir, Id-siz yenidir
            var incomingIds = dto.Items.Where(i => i.Id is > 0).Select(i => i.Id!.Value).ToHashSet();
            foreach (var stale in page.Items.Where(i => !incomingIds.Contains(i.Id)).ToList())
                _db.LinkPageItems.Remove(stale);
            foreach (var (itemDto, idx) in dto.Items.Select((x, i) => (x, i)))
            {
                var existing = itemDto.Id is > 0 ? page.Items.FirstOrDefault(i => i.Id == itemDto.Id) : null;
                if (existing == null)
                {
                    var created = ApplyItem(new LinkPageItem { LinkPage = page, RegDate = DateTime.UtcNow }, itemDto, idx);
                    page.Items.Add(created);
                }
                else
                {
                    ApplyItem(existing, itemDto, idx);
                    existing.EditDate = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync(ct);
            _cache.Remove(PublicKey(oldSlug));
            _cache.Remove(PublicKey(page.Slug));
            return await GetAsync(page.Id, ct);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var page = await _db.LinkPages.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (page == null) return ServiceResult<bool>.Fail("Səhifə tapılmadı.");
            _db.LinkPages.Remove(page); // Items + ClickEvents cascade
            await _db.SaveChangesAsync(ct);
            _cache.Remove(PublicKey(page.Slug));
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<LinkPageStatsDto>> GetStatsAsync(int id, DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            var page = await _db.LinkPages.AsNoTracking().Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, ct);
            if (page == null) return ServiceResult<LinkPageStatsDto>.Fail("Səhifə tapılmadı.");

            var end = to?.ToUniversalTime() ?? DateTime.UtcNow;
            var start = from?.ToUniversalTime() ?? end.AddDays(-30);
            var events = await _db.LinkPageClickEvents.AsNoTracking()
                .Where(e => e.LinkPageId == id && e.ClickedAt >= start && e.ClickedAt <= end)
                .Select(e => new { e.LinkPageItemId, e.ClickedAt })
                .ToListAsync(ct);

            var labels = page.Items.ToDictionary(i => i.Id, i => i.Label);
            var stats = new LinkPageStatsDto
            {
                Total = events.Count,
                ByItem = events.GroupBy(e => e.LinkPageItemId)
                    .Select(g => new LinkPageItemStatDto
                    {
                        ItemId = g.Key,
                        Label = labels.TryGetValue(g.Key, out var l) ? l : $"#{g.Key} (silinib)",
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count).ToList(),
                Series = events.GroupBy(e => DateOnly.FromDateTime(e.ClickedAt))
                    .Select(g => new LinkPageDayStatDto { Date = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Date).ToList()
            };
            return ServiceResult<LinkPageStatsDto>.Ok(stats);
        }

        // ───────────────────────── Public ─────────────────────────

        public async Task<ServiceResult<GetLinkPageDto>> GetPublicBySlugAsync(string slug, CancellationToken ct = default)
        {
            slug = (slug ?? "").Trim().ToLowerInvariant();
            if (slug.Length == 0) return ServiceResult<GetLinkPageDto>.Fail("Səhifə tapılmadı.");

            if (_cache.TryGetValue(PublicKey(slug), out GetLinkPageDto? cached) && cached != null)
                return ServiceResult<GetLinkPageDto>.Ok(cached);

            var page = await _db.LinkPages.AsNoTracking()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive && p.IsPublished, ct);
            if (page == null) return ServiceResult<GetLinkPageDto>.Fail("Səhifə tapılmadı.");

            var dto = Map(page, null, includeInactive: false);
            _cache.Set(PublicKey(slug), dto, PublicCacheTtl);
            return ServiceResult<GetLinkPageDto>.Ok(dto);
        }

        public async Task TrackClickAsync(string slug, int itemId, CancellationToken ct = default)
        {
            try
            {
                slug = (slug ?? "").Trim().ToLowerInvariant();
                var pageId = await _db.LinkPageItems.AsNoTracking()
                    .Where(i => i.Id == itemId && i.IsActive && i.LinkPage.Slug == slug && i.LinkPage.IsActive && i.LinkPage.IsPublished)
                    .Select(i => (int?)i.LinkPageId)
                    .FirstOrDefaultAsync(ct);
                if (pageId == null) return;

                _db.LinkPageClickEvents.Add(new LinkPageClickEvent
                {
                    LinkPageId = pageId.Value,
                    LinkPageItemId = itemId,
                    ClickedAt = DateTime.UtcNow,
                    RegDate = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Link page click not recorded. Slug={Slug} ItemId={ItemId}", slug, itemId);
            }
        }

        // ───────────────────────── helpers ─────────────────────────

        private async Task<string?> ValidateAsync(CreateLinkPageDto dto, int? selfId, CancellationToken ct)
        {
            dto.Slug = (dto.Slug ?? "").Trim().ToLowerInvariant();
            if (!SlugRx.IsMatch(dto.Slug)) return "Slug yalnız kiçik hərf, rəqəm və defisdən ibarət olmalıdır (2–64 simvol).";
            if (string.IsNullOrWhiteSpace(dto.Title)) return "Başlıq boşdur.";
            foreach (var (c, name) in new[] { (dto.BackgroundColor, "Fon rəngi"), (dto.TextColor, "Mətn rəngi"), (dto.PrimaryColor, "Əsas rəng") })
                if (!HexRx.IsMatch(c ?? "")) return $"{name} #RRGGBB formatında olmalıdır.";
            if (!LinkPageLayouts.All.Contains(dto.Layout)) return "Layout: list və ya grid.";
            foreach (var it in dto.Items)
            {
                if (string.IsNullOrWhiteSpace(it.Label)) return "Elementin mətni boşdur.";
                if (!Uri.TryCreate(it.Url?.Trim(), UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != "tel" && uri.Scheme != "mailto"))
                    return $"«{it.Label}»: URL https://, http://, tel: və ya mailto: ilə başlamalıdır.";
                if (!LinkPageItemStyles.All.Contains(it.Style)) return $"«{it.Label}»: stil solid / outline / ghost.";
                if (!Enum.IsDefined(it.Kind)) return $"«{it.Label}»: növ yanlışdır.";
            }
            var slugTaken = await _db.LinkPages.AnyAsync(p => p.Slug == dto.Slug && (selfId == null || p.Id != selfId), ct);
            return slugTaken ? "Bu slug artıq istifadə olunur." : null;
        }

        private static void Apply(LinkPage p, CreateLinkPageDto dto)
        {
            p.Slug = dto.Slug;
            p.Title = dto.Title.Trim();
            p.Subtitle = Clean(dto.Subtitle);
            p.LogoUrl = Clean(dto.LogoUrl);
            p.BackgroundImageUrl = Clean(dto.BackgroundImageUrl);
            p.BackgroundColor = dto.BackgroundColor.ToLowerInvariant();
            p.TextColor = dto.TextColor.ToLowerInvariant();
            p.PrimaryColor = dto.PrimaryColor.ToLowerInvariant();
            p.Layout = dto.Layout;
            p.FooterText = Clean(dto.FooterText);
            p.IsPublished = dto.IsPublished;
        }

        private static List<LinkPageItem> BuildItems(List<LinkPageItemDto> items, LinkPage page)
            => items.Select((d, idx) => ApplyItem(new LinkPageItem { LinkPage = page, RegDate = DateTime.UtcNow }, d, idx)).ToList();

        private static LinkPageItem ApplyItem(LinkPageItem e, LinkPageItemDto d, int index)
        {
            e.Kind = d.Kind;
            e.Label = d.Label.Trim();
            e.Description = Clean(d.Description);
            e.Url = d.Url.Trim();
            e.Style = d.Style;
            e.IconUrl = Clean(d.IconUrl);
            e.SortOrder = index; // göndərilən sıra = ekrandakı sıra
            e.IsActive = d.IsActive;
            return e;
        }

        private async Task<Dictionary<int, int>> ClickCountsAsync(List<int> pageIds, CancellationToken ct)
        {
            if (pageIds.Count == 0) return new();
            return await _db.LinkPageClickEvents.AsNoTracking()
                .Where(e => pageIds.Contains(e.LinkPageId))
                .GroupBy(e => e.LinkPageItemId)
                .Select(g => new { g.Key, N = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.N, ct);
        }

        private static GetLinkPageDto Map(LinkPage p, Dictionary<int, int>? clicks, bool includeInactive)
        {
            var items = p.Items
                .Where(i => includeInactive || i.IsActive)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                .Select(i => new GetLinkPageItemDto
                {
                    Id = i.Id,
                    Kind = i.Kind,
                    Label = i.Label,
                    Description = i.Description,
                    Url = i.Url,
                    Style = i.Style,
                    IconUrl = i.IconUrl,
                    SortOrder = i.SortOrder,
                    IsActive = i.IsActive,
                    ClickCount = clicks != null && clicks.TryGetValue(i.Id, out var n) ? n : 0
                }).ToList();
            return new GetLinkPageDto
            {
                Id = p.Id,
                Slug = p.Slug,
                Title = p.Title,
                Subtitle = p.Subtitle,
                LogoUrl = p.LogoUrl,
                BackgroundImageUrl = p.BackgroundImageUrl,
                BackgroundColor = p.BackgroundColor,
                TextColor = p.TextColor,
                PrimaryColor = p.PrimaryColor,
                Layout = p.Layout,
                FooterText = p.FooterText,
                IsPublished = p.IsPublished,
                IsActive = p.IsActive,
                RegDate = p.RegDate,
                EditDate = p.EditDate,
                Items = items,
                TotalClicks = items.Sum(i => i.ClickCount)
            };
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
