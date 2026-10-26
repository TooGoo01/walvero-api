namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>Brauzerdə (walvero.com) baş vermiş client-side xətanın hesabatı (anonim, fire-and-forget).</summary>
    public class ClientErrorDto
    {
        public string? Message { get; set; }
        public string? Stack { get; set; }
        public string? Url { get; set; }
        public string? UserAgent { get; set; }
        public string? Digest { get; set; }
        public string? Locale { get; set; }
        /// <summary>Hansı error boundary tutub: site / enrollment …</summary>
        public string? Source { get; set; }
    }
}
