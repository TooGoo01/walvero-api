using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class ReplyToFeedbackDto
    {
        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Message { get; set; } = string.Empty;
    }
}

