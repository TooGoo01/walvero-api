using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public static class TenantGuards
    {
        public static int EnsureTenant(ITenantProvider tp) =>
            tp.CurrentTenantId ?? throw new UnauthorizedAccessException("Tenant not resolved.");
    }
}
