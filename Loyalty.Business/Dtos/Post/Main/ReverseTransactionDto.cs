using System;
using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class ReverseTransactionDto
    {
        [Required]
        public string OrderId { get; set; } = string.Empty;
        
        [Required]
        public int TransactionId { get; set; }
        
        [Required]
        public string OriginalType { get; set; } = string.Empty; // "EARN" or "REDEEM"
        
        [Required]
        public string Reason { get; set; } = string.Empty;
        
        [Required]
        public DateTime Timestamp { get; set; }
    }
}

