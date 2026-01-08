using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class HealthCheckDto
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }
}

