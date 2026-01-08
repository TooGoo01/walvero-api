namespace Loyalty.Business.Services.Abstractions.Users
{
    public interface IOtpService
    {
        Task SendOtpAsync(int userId, string purpose, CancellationToken ct = default);
        Task<bool> ValidateOtpAsync(int userId, string purpose, string code, CancellationToken ct = default);
    }
}

