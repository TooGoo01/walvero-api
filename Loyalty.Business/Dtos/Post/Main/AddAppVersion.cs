using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddAppVersion
    {
        public string Platform { get; set; } = default!;

        public bool IsMandatory { get; set; }

        public string LatestVersion { get; set; } = default!;
        public string? Description { get; set; }
        public string? MessageTitle { get; set; }
        public string? Message { get; set; }
        public string? StoreUrl { get; set; }
    }
}
