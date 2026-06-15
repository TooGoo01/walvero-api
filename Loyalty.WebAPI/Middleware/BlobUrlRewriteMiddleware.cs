using Loyalty.Business.Services.Implementations.Storage;
using System.Text;

namespace Loyalty.WebAPI.Middleware
{
    /// <summary>
    /// Rewrites Azure Blob Storage host references inside JSON response bodies to point at the CDN
    /// endpoint when `Cdn:BaseUrl` is configured. Lets historical DB rows (stored with the raw blob
    /// host) reach clients as CDN URLs without a backfill migration.
    ///
    /// Only acts on JSON content-types and only when CDN is enabled, so the cost on non-JSON / when
    /// CDN is off is one type check.
    /// </summary>
    public sealed class BlobUrlRewriteMiddleware
    {
        private const string BlobHost = "https://walverostorage.blob.core.windows.net";

        private readonly RequestDelegate _next;
        private readonly BlobUrlRewriter _rewriter;

        public BlobUrlRewriteMiddleware(RequestDelegate next, BlobUrlRewriter rewriter)
        {
            _next = next;
            _rewriter = rewriter;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_rewriter.IsEnabled)
            {
                await _next(context);
                return;
            }

            var original = context.Response.Body;
            await using var buffer = new MemoryStream();
            context.Response.Body = buffer;

            try
            {
                await _next(context);

                var contentType = context.Response.ContentType ?? string.Empty;
                if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) && buffer.Length > 0)
                {
                    buffer.Position = 0;
                    using var reader = new StreamReader(buffer, Encoding.UTF8, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();

                    var cdnHost = _rewriter.Rewrite(BlobHost + "/__probe__");
                    // probe is `cdnHost/__probe__`; isolate CDN host prefix by trimming probe path
                    var cdnBase = cdnHost is null ? null : cdnHost.Substring(0, cdnHost.Length - "/__probe__".Length);

                    if (!string.IsNullOrEmpty(cdnBase))
                    {
                        var rewritten = body.Replace(BlobHost, cdnBase, StringComparison.OrdinalIgnoreCase);
                        var bytes = Encoding.UTF8.GetBytes(rewritten);
                        // Let downstream middleware (compression) set framing — clear stale length.
                        context.Response.ContentLength = null;
                        await original.WriteAsync(bytes);
                        return;
                    }
                }

                buffer.Position = 0;
                await buffer.CopyToAsync(original);
            }
            finally
            {
                context.Response.Body = original;
            }
        }
    }
}
