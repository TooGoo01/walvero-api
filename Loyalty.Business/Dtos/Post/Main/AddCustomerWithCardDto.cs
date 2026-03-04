namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class AddCustomerWithCardDto
    {
        public int TenantId { get; set; }
        public int LoyaltyProgramId { get; set; }
        public string? DevicePlatform { get; set; } = "";
    }
}
