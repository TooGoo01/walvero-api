using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IAppSettingService
    {
        Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct = default);
        Task<ServiceResult> GetAllAsync(CancellationToken ct = default);
        Task<ServiceResult> SetAsync(string key, string value);
    }
}
