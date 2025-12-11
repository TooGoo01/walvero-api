using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetLoyaltyObjectDto
    {
        public string Id { get; set; }
        public string ClassId { get; set; }
        public string State { get; set; }
        public string AccountId { get; set; }
        public string AccountName { get; set; }
    }
}
