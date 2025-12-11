using Loyalty.DataAccess.Models;

namespace Loyalty.Business.Dtos.Get.Users
{
    public class VerifyOtpResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserSessionDto? Session { get; set; } // Only for registration verification
    }
}

