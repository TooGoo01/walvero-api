namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>
    /// Request DTO for creating a Walvero wallet pass.
    /// </summary>
    public class CreateWalveroPassRequestDto
    {
        /// <summary>
        /// Application user ID (must match authenticated user).
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Platform: "ios" or "android"
        /// </summary>
        public string Platform { get; set; } = string.Empty;
    }
}

