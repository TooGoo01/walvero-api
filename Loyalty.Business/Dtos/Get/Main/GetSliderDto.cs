using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetSliderDto
    {
        public int Id { get; set; }
      
        public List<GetFileDto> SliderFiles { get; set; }
    }
}
