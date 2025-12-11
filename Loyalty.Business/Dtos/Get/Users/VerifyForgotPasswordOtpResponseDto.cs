namespace Loyalty.Business.Dtos.Get.Users
{
    public class VerifyForgotPasswordOtpResponseDto
    {
        public bool Success { get; set; }
        public string ResetToken { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}

