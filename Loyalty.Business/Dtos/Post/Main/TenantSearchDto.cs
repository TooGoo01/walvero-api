using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class TenantSearchDto
    {
        public int? categoryId { get; set; }
        public string? Name { get; set; }
        public int? countryId { get; set; }
        public int? cityId { get; set; }
        public int? tenantId { get; set; } // Verilibsə yalnız həmin tenant (filiallar düyməsi — bir tenant-ın branch-ları)
        public string? Platform { get; set; } // "web" → HideInApp tenant/branch-lar da görünür; digər hallarda (mobil app) gizlənir
    }
}
