using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public sealed class ProgramUiConfigDto
    {
        public bool Success { get; set; }
        public string ProgramName { get; set; } = "";
        public CardTemplateType TemplateType { get; set; }
        public bool ShowBalanceOnUi { get; set; }
        public List<TierRow> Tiers { get; set; } = new();

        public sealed class TierRow
        {
            public string Name { get; set; } = "";
            public int? Threshold { get; set; }
            public int? Percent { get; set; }
            public string? IconUrl { get; set; }
        }
    }
}
