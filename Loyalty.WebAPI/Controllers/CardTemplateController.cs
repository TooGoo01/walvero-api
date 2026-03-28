using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class CardTemplateController : ControllerBase
    {
        private readonly ICardTemplateService _cardTemplateService;
        private readonly ITenantProvider _tenantProvider;

        public CardTemplateController(ITenantProvider tenantProvider, ICardTemplateService cardTemplateService)
        {
            _tenantProvider = tenantProvider;
            _cardTemplateService = cardTemplateService;
        }
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult<List<GetCardTemplateDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> List()
        {
            var result = await _cardTemplateService.GetAll();
            return Ok(result);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Add([FromForm] AddCardTemplateDto addTenantDto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {

                foreach (var error in ModelState.Values.SelectMany(modelState => modelState.Errors))
                {
                    ModelState.AddModelError("", error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }
            var result = await _cardTemplateService.AddOrUpdateAsync(addTenantDto, ct);
            if (result.Success)
                return Ok(result);

            //ModelState.AddModelError("", "Butun hisselerin tam doldurulduqundan emin olun!");
            return BadRequest(result);
        }
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Update([FromForm] AddCardTemplateDto updateTenantDto, int Id)
        {
            if (!ModelState.IsValid)
            {
                var tenant = await _cardTemplateService.Get(Id);
                foreach (var error in ModelState.Values.SelectMany(modelState => modelState.Errors))
                {
                    ModelState.AddModelError("", error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }
            var result = await _cardTemplateService.AddOrUpdateAsync(updateTenantDto);
            if (result.Success)
                return Ok(result);

            //ModelState.AddModelError("", "Butun hisselerin tam doldurulduqundan emin olun!");
            return BadRequest(result);
        }

        [HttpDelete]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Delete(int Id)
        {
            var result = await _cardTemplateService.Delete(Id);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }
    }
}
