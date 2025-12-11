namespace Loyalty.Business.Dtos.Get.Users
{
    public class ProfileStatusDto
    {
        public bool IsGuest { get; set; }
        public bool IsProfileCompleted { get; set; }
        public bool RequiresProfileCompletion { get; set; }
    }
}

