using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Implementations.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Policy = "CustomerOnly")]
    public class CardsController : ControllerBase
    {
        private readonly IUserCardService _userCardService;

        public CardsController(IUserCardService userCardService)
        {
            _userCardService = userCardService;
        }
        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetMyCards(CancellationToken ct, [FromQuery] TenantSearchDto? model, int page = 1, int currentPage = 1, int pageSize = 10, double? lat = null, double? lng = null)
        {
            // Flutter "page", Next.js "currentPage" göndərir — hər ikisini dəstəklə
            var effectivePage = page > 1 ? page : currentPage;
            var res = await _userCardService.GetMyCardsAsync(effectivePage,pageSize,model,lat,lng,ct);

            if (!res.Success)
                return BadRequest(new { message = res.Message });

            return Ok(res.Data); // Data içində List<UserCardDto> olacaq
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetGeofencePoints(CancellationToken ct, double? lat = null, double? lng = null, double radiusKm = 25)
        {
            var res = await _userCardService.GetGeofencePointsAsync(lat, lng, radiusKm, ct);
            if (!res.Success)
                return BadRequest(new { message = res.Message });
            return Ok(res.Data); // List<GeofencePointDto>
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult>> Deactive(int id)
        {
            var result = await _userCardService.Delete(id);
            if (result.Success)
            {
                return new ServiceResult(true, result.Data, "Delete completed");
            }
            return new ServiceResult(false, result.Message);
        }

        // Discover-dən silmə: klientdə cardId olmaya bilər (lokal index boşluğu),
        // tenant üzrə cari userin aktiv kartını server tapıb deaktiv edir.
        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult>> DeactiveByTenant(int tenantId, CancellationToken ct)
        {
            var result = await _userCardService.DeleteByTenant(tenantId, ct);
            if (result.Success)
            {
                return new ServiceResult(true, result.Data, "Delete completed");
            }
            return new ServiceResult(false, result.Message);
        }
    }
}
