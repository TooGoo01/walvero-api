using Microsoft.AspNetCore.Mvc.Filters;

public sealed class EnsureTenantCookieFilter : IAsyncActionFilter
{
    private const string TenantSlugCookie = "tenant_slug";
    private const string TenantIdCookie = "tenant_id";

    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var http = ctx.HttpContext;

        // Middleware artıq buraya qədər slug/id yazıbsa götürürük
        var slugObj = http.Items.TryGetValue("TenantSlug", out var s) ? s : (http.Items.TryGetValue("TenantSubdomain", out var s2) ? s2 : null);
        var idObj = http.Items.TryGetValue("TenantId", out var i) ? i : null;

        if (slugObj is string slug && !string.IsNullOrWhiteSpace(slug))
        {
            // Mövcud cookie eyni deyilsə yenilə
            var existingSlug = http.Request.Cookies.TryGetValue(TenantSlugCookie, out var cs) ? cs : null;
            if (!string.Equals(existingSlug, slug, StringComparison.Ordinal))
            {
                http.Response.Cookies.Append(TenantSlugCookie, slug, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = http.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                    // Prod üçün ehtiyac varsa: Domain = ".sizin-domain.com"
                });
            }

            // (Opsional) TenantId-ni də yaz
            if (idObj is int id)
            {
                var existingId = http.Request.Cookies.TryGetValue(TenantIdCookie, out var ci) ? ci : null;
                if (!string.Equals(existingId, id.ToString(), StringComparison.Ordinal))
                {
                    http.Response.Cookies.Append(TenantIdCookie, id.ToString(), new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = http.Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(7)
                    });
                }
            }
        }

        await next();
    }
}
