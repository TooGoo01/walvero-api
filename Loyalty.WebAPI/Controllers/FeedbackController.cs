
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;

namespace LoyaltyWalletApp.WEBUI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class FeedbackController : ControllerBase
    {
        private readonly IFeedbackService _feedbackService;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly ITenantProvider _tenantProvider;

        public FeedbackController(
            IFeedbackService feedbackService,
            LoyaltyWalletDbContext dbContext,
            ITenantProvider tenantProvider)
        {
            _feedbackService = feedbackService;
            _dbContext = dbContext;
            _tenantProvider = tenantProvider;
        }

        // POST /api/feedback - Create feedback (Customer)
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CustomerOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateFeedback([FromBody] CreateFeedbackDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Get current user ID
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized("User not authenticated.");
            }
          
            // Get customer from ApplicationUser
            var customer = await _dbContext.Customers
        .FirstOrDefaultAsync(c => c.applicationUserId == userId && c.TenantId == dto.TenantId);


            if (customer == null)
            {
                return BadRequest("Customer profile not found.");
            }

            var result = await _feedbackService.CreateFeedbackAsync(customer.Id, dto);
            
            if (result.Success)
            {
                return Ok(result.Data);
            }

            return BadRequest(result.Message);
        }

        // GET /api/feedback/my - Get customer's feedbacks
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CustomerOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetMyFeedbacks([FromQuery] FeedbackStatus? status = null, [FromQuery] int? tenantId = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized("User not authenticated.");
            }

            // If tenantId is provided, get feedbacks for that specific tenant
            // Otherwise, get all feedbacks from all tenants
            ServiceResult result;
            
            if (tenantId.HasValue)
            {
                // Get customer for specific tenant
                var customer = await _dbContext.Customers
                    .FirstOrDefaultAsync(c => c.applicationUserId == userId && c.TenantId == tenantId.Value);

                if (customer == null)
                {
                    return BadRequest("Customer profile not found for this tenant.");
                }

                result = await _feedbackService.GetCustomerFeedbacksAsync(customer.Id, status);
            }
            else
            {
                // Get all feedbacks from all tenants for this application user
                result = await _feedbackService.GetCustomerFeedbacksByApplicationUserIdAsync(userId, status);
            }
            
            if (result.Success)
            {
                return Ok(result.Data);
            }

            return BadRequest(result.Message);
        }

        // GET /api/feedback/{id} - Get feedback details
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetFeedbackDetails(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized("User not authenticated.");
            }

            // Check if user is store admin or customer
            var user = await _dbContext.Users.FindAsync(userId);
            bool isStoreUser = user?.TenantId.HasValue == true;

            var result = await _feedbackService.GetFeedbackDetailsAsync(id, userId, isStoreUser);
            
            if (result.Success)
            {
                return Ok(result.Data);
            }

            return BadRequest(result.Message);
        }

        // POST /api/feedback/{id}/reply/store - Store replies to feedback
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> StoreReply(int id, [FromBody] ReplyToFeedbackDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized("User not authenticated.");
            }

            var result = await _feedbackService.ReplyToFeedbackAsync(userId, id, dto);
            
            if (result.Success)
            {
                return Ok(result.Data);
            }

            return BadRequest(result.Message);
        }

        // POST /api/feedback/{id}/reply/customer - Customer replies to feedback
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CustomerOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CustomerReply(int id, [FromBody] ReplyToFeedbackDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized("User not authenticated.");
            }

            // First get feedback to determine tenant
            var feedback = await _dbContext.StoreFeedbacks
                .FirstOrDefaultAsync(f => f.Id == id && f.IsActive);

            if (feedback == null)
            {
                return BadRequest("Feedback not found.");
            }

            // Get customer for the specific tenant from feedback
            var customer = await _dbContext.Customers
                .FirstOrDefaultAsync(c => c.applicationUserId == userId && c.TenantId == feedback.TenantId);
            
            if (customer == null)
            {
                return BadRequest("Customer profile not found for this tenant.");
            }

            var result = await _feedbackService.CustomerReplyAsync(customer.Id, id, dto);
            
            if (result.Success)
            {
                return Ok(result.Data);
            }

            return BadRequest(result.Message);
        }

        // POST /api/feedback/AdminCloseFeedback - Close feedback (Tenant Admin)
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> AdminCloseFeedback([FromQuery] int id)
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue)
                return BadRequest("Tenant not found.");

            var feedback = await _dbContext.StoreFeedbacks
                .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId.Value && f.IsActive);

            if (feedback == null)
                return BadRequest("Feedback not found.");

            if (feedback.Status == DataAccess.Enums.FeedbackStatus.Closed)
                return BadRequest("Feedback is already closed.");

            feedback.Status = DataAccess.Enums.FeedbackStatus.Closed;
            feedback.UpdatedAt = DateTime.UtcNow;
            feedback.EditDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Feedback closed successfully." });
        }

        // POST /api/feedback/{id}/close - Close feedback (Customer)
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CustomerOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CloseFeedback(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized("User not authenticated.");
            }

            // First get feedback to determine tenant
            var feedback = await _dbContext.StoreFeedbacks
                .FirstOrDefaultAsync(f => f.Id == id && f.IsActive);

            if (feedback == null)
            {
                return BadRequest("Feedback not found.");
            }

            // Get customer for the specific tenant from feedback
            var customer = await _dbContext.Customers
                .FirstOrDefaultAsync(c => c.applicationUserId == userId && c.TenantId == feedback.TenantId);

            if (customer == null)
            {
                return BadRequest("Customer profile not found for this tenant.");
            }

            var result = await _feedbackService.CloseFeedbackAsync(customer.Id, id);
            
            if (result.Success)
            {
                return Ok(new { message = result.Message });
            }

            return BadRequest(result.Message);
        }

        // GET /api/feedback/store - Get store feedbacks (Store admin)
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetStoreFeedbacks([FromQuery] FeedbackStatus? status = null)
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue)
            {
                return BadRequest("Tenant not found.");
            }

            var result = await _feedbackService.GetStoreFeedbacksAsync(tenantId.Value, status);
            
            if (result.Success)
            {
                return Ok(result.Data);
            }

            return BadRequest(result.Message);
        }
    }
}

