using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class LinkSessionDto
    {
        [Required]
        public string OrderId { get; set; } = string.Empty;
        
        [Required]
        public string SessionToken { get; set; } = string.Empty;
    }
}

