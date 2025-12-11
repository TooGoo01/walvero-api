using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public enum TransactionType
    {
        Earned = 1,      // Xal qazanmaq
        Redeemed = 2,    // Xal xərcləmək
        Expired = 3,     // Xal bitmə
        Adjusted = 4,    // Manual düzəliş
        Bonus = 5        // Bonus xal
    }
}
