namespace Loyalty.Business.Services.Abstractions.Main
{
    public sealed record EkassaImageResult(byte[] Bytes, string ContentType, string ResolvedUrl);

    /// <summary>
    /// e-kassa.gov.az fiscal monitoring endpoint-dən docId əsasında çekin generated
    /// JPEG/PNG image-ini fetch edir. URL template Tenant-dən gəlir (dynamic, gələcəkdə
    /// endpoint dəyişərsə admin paneldən yenilənir). Endpoint hazırda reCAPTCHA-sız açıqdır.
    /// </summary>
    public interface IEkassaImageFetcher
    {
        Task<EkassaImageResult?> FetchAsync(string docId, string urlTemplate, CancellationToken ct = default);
    }
}
