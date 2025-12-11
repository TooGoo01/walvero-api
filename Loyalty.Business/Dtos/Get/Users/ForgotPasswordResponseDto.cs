namespace Loyalty.Business.Dtos.Get.Users
{
    public class ForgotPasswordResponseDto
    {
        public bool OtpSent { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}

