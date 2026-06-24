namespace Loyalty.Business.Dtos.Get.Main
{
    // Çoxseçimli link aksiyası. Kind: whatsapp|call|sms|telegram|email|link
    public class LinkActionDto
    {
        public string Kind { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Label { get; set; }
    }
}
