using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public sealed class ImageUploadResultDto
    {
        public string Url { get; set; } = string.Empty;     // public link (DB-yə yazacağın)
        public string FileName { get; set; } = string.Empty;
    }
}
