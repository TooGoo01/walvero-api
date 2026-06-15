namespace Loyalty.Business.Services.Abstractions.Storage
{
    public record UploadedVideoResult(
        string VideoFileName,
        string VideoUrl,
        string? PosterFileName,
        string? PosterUrl);
}
