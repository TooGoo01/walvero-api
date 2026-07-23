using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Loyalty.DataAccess.Entities.Users;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
    public class RoleController : ControllerBase
    {
        private readonly RoleManager<ApplicationRole> _roleManager;

        public RoleController(RoleManager<ApplicationRole> roleManager)
        {
            _roleManager = roleManager;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var roles = await _roleManager.Roles
                .Select(r => new { r.Id, r.Name })
                .OrderBy(r => r.Name)
                .ToListAsync();
            return Ok(roles);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRoleDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Role name is required" });

            var exists = await _roleManager.RoleExistsAsync(dto.Name.Trim());
            if (exists)
                return BadRequest(new { message = "Role already exists" });

            var result = await _roleManager.CreateAsync(new ApplicationRole { Name = dto.Name.Trim() });
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });

            return Ok(new { success = true });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var role = await _roleManager.FindByIdAsync(id.ToString());
            if (role == null)
                return NotFound(new { message = "Role not found" });

            var protectedRoles = new[] { "Super Admin", "Tenant Admin", "Moderator", "Customer", "Menu Admin" };
            if (protectedRoles.Contains(role.Name))
                return BadRequest(new { message = "Cannot delete system role" });

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join(", ", result.Errors.Select(e => e.Description)) });

            return Ok(new { success = true });
        }
    }

    public class CreateRoleDto
    {
        public string Name { get; set; } = string.Empty;
    }
}
