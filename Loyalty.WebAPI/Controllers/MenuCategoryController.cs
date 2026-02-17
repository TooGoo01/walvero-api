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
    public class MenuCategoryController : ControllerBase
    {
        private readonly IMenuCategoryService _menuCategoryService;

        public MenuCategoryController(IMenuCategoryService menuCategoryService)
        {
            _menuCategoryService = menuCategoryService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetMenuCategoryDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _menuCategoryService.GetAll();
            if (result.Success)
                return Ok(result);
            
            return BadRequest(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetMenuCategoryDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetAllWithItems()
        {
            var result = await _menuCategoryService.GetAllWithItems();
            if (result.Success)
                return Ok(result);
            
            return BadRequest(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetMenuCategoryDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetActive()
        {
            var result = await _menuCategoryService.GetActive();
            if (result.Success)
                return Ok(result);
            
            return BadRequest(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ServiceResult<GetMenuCategoryDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _menuCategoryService.GetById(id);
            if (result.Success)
                return Ok(result);
            
            return NotFound(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ServiceResult<GetMenuCategoryDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetByIdWithItems(int id)
        {
            var result = await _menuCategoryService.GetByIdWithItems(id);
            if (result.Success)
                return Ok(result);
            
            return NotFound(result);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult<GetMenuCategoryDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Add([FromBody] AddMenuCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _menuCategoryService.Add(dto);
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpPut]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult<GetMenuCategoryDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Update([FromBody] UpdateMenuCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _menuCategoryService.Update(dto);
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
            var result = await _menuCategoryService.Delete(id);
            if (result.Success)
                return Ok(result);

            return NotFound(result);
        }
    }
}
