using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Realtime
{
    public class CardUpdatedMessage
    {
        public int CardId { get; set; }
        public int ProgramId { get; set; }
        public int TenantId { get; set; }

        public decimal CurrentPoints { get; set; }

        // istəsən əlavə:
        public decimal? LastRedeemAmount { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
