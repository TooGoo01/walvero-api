using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class DeviceTokenService : IDeviceTokenService
    {
        private readonly LoyaltyWalletDbContext _db;

        public DeviceTokenService(LoyaltyWalletDbContext db)
        {
            _db = db;
        }

        public async Task RegisterAsync(RegisterDeviceTokenRequest request)
        {
            // Guest mode: mobile userId="0" göndərir (login olmamış istifadəçilər üçün).
            // AspNetUsers-də id=0 olan user yoxdur — FK violation-dan qorumaq üçün
            // 0 və ya invalid id-ləri null-a çeviririk.
            int? userId = NormalizeUserId(request.UserId);

            // Əgər non-null user id varsa, mövcudluğunu yoxla — silinmiş user gəlsə
            // FK yenə pozular. Mövcud deyilsə guest kimi yaz.
            if (userId.HasValue)
            {
                var userExists = await _db.Users.AnyAsync(u => u.Id == userId.Value);
                if (!userExists)
                {
                    userId = null;
                }
            }

            var existing = await _db.DeviceTokens
                .FirstOrDefaultAsync(x => x.Token == request.Token);

            if (existing == null)
            {
                _db.DeviceTokens.Add(new DeviceToken
                {
                    UserId = userId,
                    Token = request.Token,
                    Platform = request.Platform
                });
            }
            else
            {
                existing.UserId = userId;
                existing.Platform = request.Platform;
            }

            await _db.SaveChangesAsync();
        }

        public async Task RemoveAsync(RemoveDeviceTokenRequest request)
        {
            int? userId = NormalizeUserId(request.UserId);

            // Guest token (UserId=null) silmək üçün yalnız Token üzrə axtar;
            // login-li token üçün UserId + Token cütlüyü ilə.
            var query = userId.HasValue
                ? _db.DeviceTokens.Where(x => x.UserId == userId.Value && x.Token == request.Token)
                : _db.DeviceTokens.Where(x => x.UserId == null && x.Token == request.Token);

            var entity = await query.FirstOrDefaultAsync();
            if (entity != null)
            {
                _db.DeviceTokens.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        /// <summary>0 və ya mənfi dəyər guest sayılır → null qaytarır.</summary>
        private static int? NormalizeUserId(int userId) => userId > 0 ? userId : null;
    }
}
