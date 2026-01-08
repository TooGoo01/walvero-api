using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class ScannerSessionResponseDto
    {
        public string SessionToken { get; set; } = string.Empty;
        public long? WalveroUserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int LoyaltyBalance { get; set; }
        public DateTime SessionExpiry { get; set; }
        public SessionStatus Status { get; set; }
    }
}

