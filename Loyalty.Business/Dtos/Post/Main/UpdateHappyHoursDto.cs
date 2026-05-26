using System.Collections.Generic;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class UpdateHappyHoursDto
    {
        public int? BasePercentCash { get; set; }
        public int? BasePercentCard { get; set; }
        public List<HappyHoursWindowDto> HappyHoursWindows { get; set; } = new();
    }
}
