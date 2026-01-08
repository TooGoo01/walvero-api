namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>
    /// Response DTO for Walvero wallet pass creation.
    /// </summary>
    public class WalveroWalletPassResponseDto
    {
        public int TenantId { get; set; }
        public int UserId { get; set; }
        public string Platform { get; set; } = string.Empty;
        public string WalveroIdMasked { get; set; } = string.Empty;
        public string? ApplePassUrl { get; set; }
        public string? GoogleSaveUrl { get; set; }
        public int PassId { get; set; }
    }
}

