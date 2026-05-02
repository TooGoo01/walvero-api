namespace Loyalty.Business.Dtos.Get.Users
{
    public class UpdateProfileResponseDto
    {
        public bool RequiresOldPhoneVerification { get; set; }
        public bool RequiresNewPhoneVerification { get; set; }
        public string? Message { get; set; }
        public string? PendingNewPhoneNumber { get; set; } // Yeni telefon nömrəsi (təsdiqlənmə gözləyir)
        public bool IsProfileCompleted { get; set; }       // Update sonrası profilin tam olduğunu göstərir
    }
}
