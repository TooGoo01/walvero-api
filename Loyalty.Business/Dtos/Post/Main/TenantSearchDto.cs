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
        public string? userName { get; set; }
        public string? Name { get; set; }
    }
}
