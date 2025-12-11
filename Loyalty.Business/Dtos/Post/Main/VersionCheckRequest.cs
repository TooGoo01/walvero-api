using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class VersionCheckRequest
    {
        public string Platform { get; set; } = default!; // "android" / "ios"
        public string CurrentVersion { get; set; } = default!;
    }
}
