using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetProductDto
    {
        public int Id { get; set; }
        public string AzTitle { get; set; }
        public string AzDescription { get; set; }
        public string EnTitle { get; set; }
        public string EnDescription { get; set; }
        public string RuTitle { get; set; }
        public string RuDescription { get; set; }
        public int ServiceId { get; set; }
        public GetServiceDto Service { get; set; }

        public bool IsActive { get; set; }
        public ICollection<GetFileDto> ProductFiles { get; set; }
    }
}
