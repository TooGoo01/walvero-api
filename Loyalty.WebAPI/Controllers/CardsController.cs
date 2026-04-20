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
        public async Task<IActionResult> GetMyCards(CancellationToken ct, [FromQuery] TenantSearchDto? model, int page = 1, int currentPage = 1, int pageSize = 10)
        {
            // Flutter "page", Next.js "currentPage" göndərir — hər ikisini dəstəklə
            var effectivePage = page > 1 ? page : currentPage;
            var res = await _userCardService.GetMyCardsAsync(effectivePage,pageSize,model,ct);

            if (!res.Success)
                return BadRequest(new { message = res.Message });

            return Ok(res.Data); // Data içində List<UserCardDto> olacaq
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
    }
}
