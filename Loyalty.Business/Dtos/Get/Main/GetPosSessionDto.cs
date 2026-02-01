using Loyalty.DataAccess.Enums;
using System;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetPosSessionDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string ScannerDeviceId { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int? OrderId { get; set; }
        public int? TableId { get; set; }
        public string? Barcode { get; set; }
        public SessionStatus Status { get; set; }
        public string? SessionToken { get; set; }
        public DateTime RegDate { get; set; }
        public DateTime? EditDate { get; set; }
        public bool IsActive { get; set; }
    }
}

