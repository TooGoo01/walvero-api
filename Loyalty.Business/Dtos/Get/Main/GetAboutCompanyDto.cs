namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetAboutCompanyDto
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Adress { get; set; }
        public bool IsActive { get; set; }
        public string MapAddress { get; set; }
        public string? InstagramLink { get; set; }
        public string? FacebookLink { get; set; }
        public string? TwitterLink { get; set; }
        public string? LinkedinLink { get; set; }
    }
}
