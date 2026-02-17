using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddRestorantWifiInformationDto
    {
        [Required(ErrorMessage = "Parol məcburidir")]
        public string Password { get; set; }

        [Required(ErrorMessage = "SSID məcburidir")]
        public string SSID { get; set; }

        [Required(ErrorMessage = "Tenant məcburidir")]
        public int TenantId { get; set; }
    }
}
