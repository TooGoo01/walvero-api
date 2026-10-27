namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Öz Azure Blob hostlarımız. Cari host `Storage:Azure` connection string-dən (AccountName / BlobEndpoint) gəlir;
    /// köhnə hostlar (`Storage:LegacyHosts`, vergüllə) DB-də saxlanmış köhnə URL-lər üçün alias kimi tanınır.
    /// Storage hesabı köçürüləndə (walverostorage → walverostoragepl) köhnə URL-lər CDN-ə yazılır və eyni
    /// konteyner/blob adı ilə yeni hesabdan oxunur — DB-də URL backfill lazım deyil.
    /// Startup-da bir dəfə <see cref="Configure"/> çağırılır; çağırılmasa (testlər) default köhnə host işləyir.
    /// </summary>
    public static class StorageHosts
    {
        public const string DefaultHost = "walverostorage.blob.core.windows.net";

        /// <summary>Yeni yüklənən faylların URL hostu.</summary>
        public static string CurrentHost { get; private set; } = DefaultHost;

        /// <summary>Cari + köhnə hostlar (təkrarsız, cari birinci).</summary>
        public static IReadOnlyList<string> BlobHosts { get; private set; } = new[] { DefaultHost };

        public static string CurrentBaseUrl => "https://" + CurrentHost;

        public static void Configure(string? storageConnectionString, string? legacyHosts)
        {
            var current = ParseAccountHost(storageConnectionString) ?? DefaultHost;
            CurrentHost = current;
            BlobHosts = Build(current, legacyHosts);
        }

        /// <summary>Cari host + köhnələr (default köhnə host həmişə daxildir), təkrarsız.</summary>
        public static IReadOnlyList<string> Build(string currentHost, string? legacyHosts)
        {
            var list = new List<string> { currentHost };
            var legacy = (legacyHosts ?? string.Empty)
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeHost)
                .Where(h => h.Length > 0)
                .Append(DefaultHost);
            foreach (var h in legacy)
                if (!list.Contains(h, StringComparer.OrdinalIgnoreCase)) list.Add(h);
            return list;
        }

        /// <summary>
        /// Connection string-dən blob hostu: `BlobEndpoint=` varsa onun hostu, yoxdursa
        /// `AccountName` + `.blob.` + `EndpointSuffix` (default core.windows.net). Tapılmasa null.
        /// </summary>
        public static string? ParseAccountHost(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return null;
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=', 2))
                .Where(kv => kv.Length == 2)
                .GroupBy(kv => kv[0].Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First()[1].Trim(), StringComparer.OrdinalIgnoreCase);

            if (parts.TryGetValue("BlobEndpoint", out var endpoint)
                && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
                return uri.Host.ToLowerInvariant();

            if (!parts.TryGetValue("AccountName", out var account) || string.IsNullOrWhiteSpace(account)) return null;
            var suffix = parts.TryGetValue("EndpointSuffix", out var s) && !string.IsNullOrWhiteSpace(s) ? s : "core.windows.net";
            return $"{account}.blob.{suffix}".ToLowerInvariant();
        }

        public static bool IsOwnBlobHost(string? host)
            => !string.IsNullOrEmpty(host) && BlobHosts.Any(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase));

        /// <summary>URL öz blob hostlarımızdan birindədir (https/http, istənilən path).</summary>
        public static bool IsOwnBlobUrl(string? url)
            => !string.IsNullOrWhiteSpace(url)
               && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
               && IsOwnBlobHost(uri.Host);

        private static string NormalizeHost(string raw)
        {
            var v = raw.Trim();
            if (Uri.TryCreate(v, UriKind.Absolute, out var uri)) return uri.Host.ToLowerInvariant();
            return v.TrimEnd('/').ToLowerInvariant();
        }
    }
}
