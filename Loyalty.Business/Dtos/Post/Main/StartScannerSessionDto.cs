using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class StartScannerSessionDto
    {
        [Required]
        public int TenantId { get; set; }
        
        [Required]
        public string ScannerDeviceId { get; set; } = string.Empty;
        
        [Required]
        public int UserId { get; set; }
        
        public int? TableId { get; set; }
        
        [Required]
        public string Barcode { get; set; } = string.Empty;
        
        [Required]
        public DateTime Timestamp { get; set; }
    }
}

