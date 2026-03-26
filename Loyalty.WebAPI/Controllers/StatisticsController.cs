using Loyalty.Business.Dtos.Get.Main;
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
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
    public class StatisticsController : ControllerBase
    {
        private readonly IStatisticsService _statisticsService;
        private readonly ITenantProvider _tenantProvider;

        public StatisticsController(
            IStatisticsService statisticsService,
            ITenantProvider tenantProvider)
        {
            _statisticsService = statisticsService;
            _tenantProvider = tenantProvider;
        }

        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(ServiceResult<DashboardStatisticsDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Dashboard(DateOnly? startDate, DateOnly? endDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var effectiveEndDate = endDate ?? today;
            var effectiveStartDate = startDate ?? effectiveEndDate.AddDays(-365);

            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue)
                return BadRequest(new ServiceResult(false, "Tenant tapılmadı."));

            var programId = _tenantProvider.CurrentProgramId;

            var result = await _statisticsService.GetDashboardAsync(
                tenantId.Value,
                effectiveStartDate,
                effectiveEndDate,
                programId);

            return Ok(result);
        }
    }
}
