using System;
using System.Collections.Generic;

namespace Loyalty.Business.Dtos.Get.Main.Room
{
    public class GetRoomDto
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? Description { get; set; }
        public int? Capacity { get; set; }
        public decimal? MinDeposit { get; set; }
        public int OrderIndex { get; set; }
        public bool IsActive { get; set; }
        public DateTime RegDate { get; set; }
        public int MediaCount { get; set; }
        public List<GetRoomFileDto> Files { get; set; } = new();
    }
}
