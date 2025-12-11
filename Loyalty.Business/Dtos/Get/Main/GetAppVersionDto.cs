using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetAppVersionDto
    {
        public int Id { get; set; }
        public string Platform { get; set; } = default!;

        public bool IsActive { get; set; }
        public bool IsMandatory { get; set; }

        public string LatestVersion { get; set; } = default!;
        public string? Description { get; set; }
        public string? MessageTitle { get; set; }
        public string? Message { get; set; }
        public string? StoreUrl { get; set; }
    }
}
