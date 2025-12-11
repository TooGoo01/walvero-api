using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ITenantProvider
    {
        int? CurrentTenantId { get; }
        int? CurrentProgramId { get; }
        string? CurrentUserId { get; }
    }
}
