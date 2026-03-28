using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetStoryDto
    {
        public int Id { get; set; }
        public string? AzTitle { get; set; }
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }
        public string? SmallImageUrl { get; set; }
        public string? ImageUrl { get; set; }
        public string? VideoUrl { get; set; }
        public int? TenantId { get; set; }
        public bool IsActive { get; set; }
    }
}

