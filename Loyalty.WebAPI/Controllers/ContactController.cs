using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContactController : ControllerBase
    {
        private readonly IContactService _contactService;

        public ContactController(IContactService contactService)
        {
            _contactService = contactService;
        }

        /// <summary>Public: web contact formundan mesaj göndər (anonim).</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Create([FromBody] AddContactDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _contactService.AddContact(dto);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }

        /// <summary>Super admin: web contact mesajlarının siyahısı.</summary>
        [HttpGet("List")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> List()
        {
            var result = await _contactService.GetContacts();
            return Ok(result);
        }

        /// <summary>Super admin: contact mesajını sil.</summary>
        [HttpDelete("{id:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _contactService.DeleteContactById(id);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }
    }
}
