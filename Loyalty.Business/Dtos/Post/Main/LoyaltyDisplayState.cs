using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class LoyaltyDisplayState
    {
        public string Header { get; set; } = "";     // “7 Steps = 1 Reward”, “Balance: 120 pts”...
        public string Body { get; set; } = "";     // emoji progress və ya izahat
        public string? CurrentIconUrl { get; set; }  // progress 0-cı/1-ci... və ya tier ikonu
        public decimal Current { get; set; }             // steps və ya points
        public int Total { get; set; }             // steps total və ya redeem limiti
        public string? TierName { get; set; }        // Bronze/Silver/Gold...
    }
}
