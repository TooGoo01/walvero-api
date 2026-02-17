using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class RestorantWifiInformationController : ControllerBase
    {
        private readonly IRestorantWifiInformationService _restorantWifiInformationService;

        public RestorantWifiInformationController(IRestorantWifiInformationService restorantWifiInformationService)
        {
            _restorantWifiInformationService = restorantWifiInformationService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetRestorantWifiInformationDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _restorantWifiInformationService.GetAll();
            if (result.Success)
                return Ok(result);
            
            return BadRequest(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetRestorantWifiInformationDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetAllWithTenant()
        {
            var result = await _restorantWifiInformationService.GetAllWithTenant();
            if (result.Success)
                return Ok(result);
            
            return BadRequest(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ServiceResult<GetRestorantWifiInformationDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _restorantWifiInformationService.GetById(id);
            if (result.Success)
                return Ok(result);
            
            return NotFound(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ServiceResult<GetRestorantWifiInformationDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetByIdWithTenant(int id)
        {
            var result = await _restorantWifiInformationService.GetByIdWithTenant(id);
            if (result.Success)
                return Ok(result);
            
            return NotFound(result);
        }

        [HttpGet("{tenantId}")]
        [ProducesResponseType(typeof(ServiceResult<List<GetRestorantWifiInformationDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetByTenantId(int tenantId)
        {
            var result = await _restorantWifiInformationService.GetByTenantId(tenantId);
            if (result.Success)
                return Ok(result);
            
            return BadRequest(result);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult<GetRestorantWifiInformationDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Add([FromBody] AddRestorantWifiInformationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _restorantWifiInformationService.Add(dto);
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpPut]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult<GetRestorantWifiInformationDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Update([FromBody] UpdateRestorantWifiInformationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _restorantWifiInformationService.Update(dto);
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpDelete("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _restorantWifiInformationService.Delete(id);
            if (result.Success)
                return Ok(result);

            return NotFound(result);
        }
    }
}
