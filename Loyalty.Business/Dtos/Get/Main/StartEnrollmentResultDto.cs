using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public sealed class StartEnrollmentResultDto
    {
        public int CustomerId { get; set; }
        public string? MaskedDestination { get; set; }  // sms/email mask
        public int CardId { get; set; }
        public int ProgramId { get; set; }
    }
}
