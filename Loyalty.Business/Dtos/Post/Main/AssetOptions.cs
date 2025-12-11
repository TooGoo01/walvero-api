using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class AssetOptions
    {
        public string RootFolder { get; set; } = "uploads";      // wwwroot/uploads
        public long MaxBytes { get; set; } = 2 * 1024 * 1024;    // 2 MB
        public string[] AllowedExt { get; set; } = [".png", ".jpg", ".jpeg", ".webp"]; // (istəsən .svg əlavə et)
    }
}
