using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetPartnerDto
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }
        public ICollection<GetFileDto> PartnerFiles { get; set; }
    }
}
