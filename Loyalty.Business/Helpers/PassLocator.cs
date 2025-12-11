using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Helpers
{
    public static class PassLocator
    {
        public static string? FindLatestPkpass(string webRootPath, int tenantId, int programId)
        {
            var dir = Path.Combine(webRootPath, "passes",
                                   tenantId.ToString(),
                                   programId.ToString());

            if (!Directory.Exists(dir)) return null;

            return Directory.EnumerateFiles(dir, "*.pkpass")
                            .OrderByDescending(f => f, System.StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault();
        }
    }
}
