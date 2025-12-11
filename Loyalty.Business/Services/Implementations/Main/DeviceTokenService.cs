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
            var existing = await _db.DeviceTokens
            .FirstOrDefaultAsync(x => x.Token == request.Token);

            if (existing == null)
            {
                // Yeni cihaz
                var token = new DeviceToken
                {
                    UserId = request.UserId,
                    Token = request.Token,
                    Platform = request.Platform
                };

                _db.DeviceTokens.Add(token);
            }
            else
            {
                // Mövcud cihaz — user dəyişibsə update et
                existing.UserId = request.UserId;
                existing.Platform = request.Platform;
            }

            await _db.SaveChangesAsync();
        }

        public async Task RemoveAsync(RemoveDeviceTokenRequest request)
        {
            var entity = await _db.DeviceTokens
           .FirstOrDefaultAsync(x => x.UserId == request.UserId && x.Token == request.Token);

            if (entity != null)
            {
                _db.DeviceTokens.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }
    }
}
