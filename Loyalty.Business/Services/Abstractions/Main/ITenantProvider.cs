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
        int? CurrentBranchId { get; }
        string? CurrentUserId { get; }

        /// <summary>Request dili: Accept-Language-in ilk 2 hərfi ("az"|"en"|"ru"), default "az".</summary>
        string CurrentLanguage { get; }
    }
}
