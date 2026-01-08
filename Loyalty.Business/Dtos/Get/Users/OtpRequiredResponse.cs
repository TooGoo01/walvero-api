namespace Loyalty.Business.Dtos.Get.Users
{
    public class OtpRequiredResponse
    {
        public int UserId { get; set; }
        public bool OtpRequired { get; set; }
        public bool OtpSent { get; set; }
        public string Purpose { get; set; } = string.Empty;
    }
}

