using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class AppSettingService : IAppSettingService
    {
        private readonly LoyaltyWalletDbContext _db;

        public AppSettingService(LoyaltyWalletDbContext db)
        {
            _db = db;
        }

        public async Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct = default)
        {
            var value = await _db.AppSettings.AsNoTracking()
                .Where(s => s.Key == key)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(ct);

            if (value == null) return defaultValue;
            return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
        }

        public async Task<ServiceResult> GetAllAsync(CancellationToken ct = default)
        {
            var settings = await _db.AppSettings.AsNoTracking()
                .OrderBy(s => s.Key)
                .Select(s => new { s.Key, s.Value, s.UpdatedAt })
                .ToListAsync(ct);

            return new ServiceResult(true, settings);
        }

        public async Task<ServiceResult> SetAsync(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return new ServiceResult(false, "Key boş ola bilməz");

            var entity = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (entity == null)
            {
                entity = new AppSetting
                {
                    Key = key,
                    Value = value,
                    UpdatedAt = DateTime.UtcNow,
                    RegDate = DateTime.UtcNow
                };
                _db.AppSettings.Add(entity);
            }
            else
            {
                entity.Value = value;
                entity.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return new ServiceResult(true, new { entity.Key, entity.Value }, "Tənzimləmə yeniləndi");
        }
    }
}
