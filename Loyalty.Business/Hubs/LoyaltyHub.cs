using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Loyalty.Business.Hubs
{
    [Authorize(
   AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
   Policy = "CustomerOnly")]
    public class LoyaltyHub : Hub
    {
        private readonly ILogger<LoyaltyHub> _logger;

        public LoyaltyHub(ILogger<LoyaltyHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            // 2) Yoxdursa, məsələn "userName" və ya "phone" custom claim-dən götür
            

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var groupName = $"user-{userId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            }
            _logger.LogInformation(
          "🔌 LoyaltyHub: Connected. ConnectionId={ConnectionId}, User={User}",
          Context.ConnectionId,
          userId
      );

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var groupName = $"user-{userId}";
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            }
            _logger.LogInformation(
            "❌ LoyaltyHub: Disconnected. ConnectionId={ConnectionId}, User={User}, Error={Error}",
            Context.ConnectionId,
            userId,
            exception?.Message
        );

            await base.OnDisconnectedAsync(exception);
        }
    }
}
