using System.ComponentModel.DataAnnotations;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class CreateFeedbackDto
    {
        [Required]
        public int TenantId { get; set; }

        [Required]
        public FeedbackType Type { get; set; }

        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Message { get; set; } = string.Empty;

        [Range(0.0, 5.0)]
        public double? Rating { get; set; }

        public bool SharePhoneNumber { get; set; }
    }
}

