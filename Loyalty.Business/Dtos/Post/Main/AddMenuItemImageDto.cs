using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddMenuItemImageDto
    {
        [Required(ErrorMessage = "Fayl adı məcburidir")]
        public string FileName { get; set; }

        [Required(ErrorMessage = "Yol məcburidir")]
        public string Path { get; set; }

        [Required(ErrorMessage = "Menu item məcburidir")]
        public int MenuItemId { get; set; }
    }
}
