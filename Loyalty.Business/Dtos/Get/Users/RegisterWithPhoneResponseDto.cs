namespace Loyalty.Business.Dtos.Get.Users
{
    public class RegisterWithPhoneResponseDto
    {
        public int UserId { get; set; }
        public bool OtpSent { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

