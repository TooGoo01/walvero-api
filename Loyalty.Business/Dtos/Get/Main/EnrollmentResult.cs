using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public sealed class EnrollmentResult
    {
        public int CustomerId { get; set; }
        public int CardId { get; set; }
        public string? GoogleSaveUrl { get; set; } // Android
        public string? ApplePassUrl { get; set; } // iOS
        public string CustomerFullName { get; set; } = "";
    }
}
