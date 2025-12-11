using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public sealed class VerifyEnrollmentResultDto
    {
        public string? GoogleSaveUrl { get; set; }
        public string? ApplePassUrl { get; set; }
        public int CardId { get; set; }
    }
}
