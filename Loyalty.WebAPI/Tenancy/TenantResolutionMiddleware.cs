//using System.Text.RegularExpressions;
//using LoyaltyWalletApp.DataAccess.Repositories.Abstractions.Main;
//using Microsoft.Extensions.Caching.Memory;

//public sealed class TenantResolutionMiddleware
//{
//    private readonly RequestDelegate _next;

//    // Slug regex (a-z0-9 və tire, 2-32 simvol arası)
//    private static readonly Regex SlugRegex = new(@"^[a-z0-9](?:[a-z0-9\-]{0,30}[a-z0-9])?$", RegexOptions.Compiled);

//    // Path prefiksləri: /t/{slug} və /tenant/{slug}
//    private static readonly string[] PathPrefixes = new[] { "t" };

//    // Cookie adları
//    private const string TenantSlugCookie = "tenant_slug";
//    private const string TenantIdCookie = "tenant_id"; // opsional

//    // Rezerv subdomain-lər
//    private static readonly HashSet<string> ReservedSubs = new(StringComparer.OrdinalIgnoreCase)
//    { "www", "api", "admin" };

//    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

//    public async Task InvokeAsync(HttpContext ctx, ITenantRepository tenants, IMemoryCache cache)
//    {
//        var ct = ctx.RequestAborted;

//        // ---- 0) Host-u düzgün oxu (proxy arxasında ola bilər) ----
//        var hostHeader =
//            (ctx.Request.Headers.TryGetValue("X-Forwarded-Host", out var fwdHost) && !string.IsNullOrWhiteSpace(fwdHost))
//            ? fwdHost.ToString()
//            : ctx.Request.Host.Value; // "shop1.example.com:5001"
//        var hostOnly = hostHeader.Split(':')[0]; // portu at
//        var parts = hostOnly.Split('.', StringSplitOptions.RemoveEmptyEntries);

//        // ---- 1) Slug axtar: subdomain → header → query → path ----
//        string? slug = null;

//        // 1.a) *.localhost (shop1.localhost)
//        if (hostOnly.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) && parts.Length >= 2)
//        {
//            slug = parts[0];
//        }
//        // 1.b) Normal prod domain (shop1.example.com) → ən azı 3 label
//        else if (parts.Length >= 3)
//        {
//            var first = parts[0];
//            if (!ReservedSubs.Contains(first))
//                slug = first;
//        }

//        // 1.c) Header (X-Tenant-Subdomain)
//        if (string.IsNullOrWhiteSpace(slug) &&
//            ctx.Request.Headers.TryGetValue("X-Tenant-Subdomain", out var hv) &&
//            !string.IsNullOrWhiteSpace(hv))
//        {
//            slug = hv.ToString();
//        }

//        // 1.d) Query (?tenant= / ?t=)
//        if (string.IsNullOrWhiteSpace(slug))
//        {
//            var q = ctx.Request.Query;
            
//             if (q.TryGetValue("t", out var tv2) && !string.IsNullOrWhiteSpace(tv2))
//                slug = tv2.ToString();
//        }

//        // 1.e) PATH: /t/{slug}/... və ya /tenant/{slug}/...
//        if (string.IsNullOrWhiteSpace(slug))
//        {
//            var seg = ctx.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
//            if (seg.Length >= 2 && PathPrefixes.Contains(seg[0], StringComparer.OrdinalIgnoreCase))
//            {
//                slug = seg[1];
//                // rewrite: /t/{slug}/qalan → /qalan (routing rahat işləsin)
//                var rest = "/" + string.Join('/', seg.Skip(2));
//                ctx.Request.Path = new PathString(rest);
//                // İstəsən PathBase istifadə edə bilərsən:
//                // ctx.Request.PathBase = new PathString($"/{seg[0]}/{slug}");
//            }
//        }

//        // 1.f) COOKIE FALLBACK — BURA ƏLAVƏ OLUNDU
//        if (string.IsNullOrWhiteSpace(slug))
//        {
//            if (ctx.Request.Cookies.TryGetValue(TenantSlugCookie, out var cs) && !string.IsNullOrWhiteSpace(cs))
//                slug = cs.Trim().ToLowerInvariant();
//        }

//        // ---- 2) Slug yoxlaması ----
//        if (string.IsNullOrWhiteSpace(slug))
//        {
//            // Tenant tələb olunmayan səhifələr ola bilər → davam et
//            await _next(ctx);
//            return;
//        }

//        slug = slug.Trim().ToLowerInvariant();
//        if (!SlugRegex.IsMatch(slug))
//        {
//            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
//            await ctx.Response.WriteAsync("Invalid tenant slug.");
//            return;
//        }

//        // ---- 3) Cache + DB ----
//        var cacheKey = $"tenant:slug:{slug}";
//        var tenant = await cache.GetOrCreateAsync(cacheKey, async entry =>
//        {
//            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

//            var t = await tenants.GetBySubdomainAsync(slug, ct); // ← səndə adı fərqlidirsə dəyiş
//            if (t is null)
//                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30); // negative cache

//            return t;
//        });

//        if (tenant is null)
//        {
//            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
//            await ctx.Response.WriteAsync("Tenant not found.");
//            return;
//        }

//        // ---- 4) HttpContext-ə yaz ----
//        ctx.Items["TenantSlug"] = slug;
//        ctx.Items["TenantId"] = tenant.Id;
//        ctx.Items["Tenant"] = tenant;

//        // RouteData-ya da yazmaq istəyirsənsə:
//        var rvd = ctx.GetRouteData()?.Values;
//        if (rvd is not null && !rvd.ContainsKey("tenant"))
//            rvd["tenant"] = slug;

//        // ---- 5) COOKIE-YƏ YAZ — BURA ƏLAVƏ OLUNDU ----
//        var existSlug = ctx.Request.Cookies.TryGetValue(TenantSlugCookie, out var ex) ? ex : null;
//        if (!string.Equals(existSlug, slug, StringComparison.Ordinal))
//        {
//            ctx.Response.Cookies.Append(TenantSlugCookie, slug, new CookieOptions
//            {
//                HttpOnly = true,
//                Secure = ctx.Request.IsHttps,
//                SameSite = SameSiteMode.Lax,
//                Expires = DateTimeOffset.UtcNow.AddDays(7)
//                // Prod-da subdomain-lər üçün domain verə bilərsən:
//                // Domain = ".sənin-domainin.com"
//            });
//        }

//        // (opsional) TenantId-ni də yazmaq istəyirsənsə:
//        var existId = ctx.Request.Cookies.TryGetValue(TenantIdCookie, out var ei) ? ei : null;
//        if (!string.Equals(existId, tenant.Id.ToString(), StringComparison.Ordinal))
//        {
//            ctx.Response.Cookies.Append(TenantIdCookie, tenant.Id.ToString(), new CookieOptions
//            {
//                HttpOnly = true,
//                Secure = ctx.Request.IsHttps,
//                SameSite = SameSiteMode.Lax,
//                Expires = DateTimeOffset.UtcNow.AddDays(7)
//            });
//        }

//        // ---- 6) DEBUG header-lar (yalnız Development) ----
//#if DEBUG
//        ctx.Response.Headers["X-Debug-TenantSlug"] = slug;
//        ctx.Response.Headers["X-Debug-TenantId"] = tenant.Id.ToString();
//#endif

//        await _next(ctx);
//    }
//}
