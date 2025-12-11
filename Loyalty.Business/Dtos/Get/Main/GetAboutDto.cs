using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Dtos.Get.Main
{
	public class GetAboutDto
	{
        public int Id { get; set; }
        public string AzTitle { get; set; }
        public string AzDescription { get; set; }
        public string EnTitle { get; set; }
        public string EnDescription { get; set; }
        public string RuTitle { get; set; }
        public string RuDescription { get; set; }
        public string? Twitter { get; set; }
        public string? Facebook { get; set; }
        public string? Instagram { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public bool IsActive { get; set; }
        public List<GetFileDto> AboutFiles { get; set; }
    }
}
