using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/pos")]
    [ApiController]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "TenantAdminOrModerator")]
    public class PosController : ControllerBase
    {
        private readonly ITransactionReverseService _transactionReverseService;
        private readonly IPosSessionService _posSessionService;
        private readonly ILogger<PosController> _logger;
        private readonly IConfiguration _configuration;

        public PosController(
            ITransactionReverseService transactionReverseService,
            IPosSessionService posSessionService,
            ILogger<PosController> logger,
            IConfiguration configuration)
        {
            _transactionReverseService = transactionReverseService;
            _posSessionService = posSessionService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Reverse a loyalty transaction
        /// </summary>
        [HttpPost("loyalty/reverse")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ReverseTransaction([FromBody] ReverseTransactionDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation(
                "Reversing transaction. TransactionId={TransactionId}, BillId={BillId}, OriginalType={OriginalType}, IdempotencyKey={IdempotencyKey}",
                dto.TransactionId, dto.OrderId, dto.OriginalType, dto.IdempotencyKey);

            var result = await _transactionReverseService.ReverseTransactionAsync(dto, ct);

            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Link a POS session with an order
        /// </summary>
        [HttpPost("session/link")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> LinkSession([FromBody] LinkSessionDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation(
                "Linking session. OrderId={OrderId}, SessionToken={SessionToken}",
                dto.OrderId, dto.SessionToken);

            var result = await _posSessionService.LinkSessionAsync(dto, ct);

            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Health check endpoint - validates authentication token from Authorization header
        /// </summary>
        [HttpPost("health")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(HealthCheckResponseDto), (int)HttpStatusCode.OK)]
        public IActionResult HealthCheck()
        {
            bool isAuthenticated = false;

            // Check if user is authenticated (token validated by JWT middleware from Authorization header)
            if (User?.Identity?.IsAuthenticated == true)
            {
                isAuthenticated = true;
            }
            else
            {
                // If not authenticated via middleware, manually validate token from Authorization header
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = authHeader.Substring("Bearer ".Length).Trim();
                    isAuthenticated = ValidateToken(token);
                }
            }

            var response = new HealthCheckResponseDto
            {
                Success = isAuthenticated,
                Status = isAuthenticated ? "Authenticated" : "Not Authenticated"
            };

            return Ok(response);
        }

        private bool ValidateToken(string token)
        {
            try
            {
                var tokenOptions = _configuration.GetSection("TokenOptions").Get<Loyalty.Infrastructure.Utilities.Security.Jwt.TokenOptions>();
                if (tokenOptions == null)
                {
                    return false;
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var securityKey = SecurityKeyHelper.CreateSecurityKey(tokenOptions.SecurityKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = tokenOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = tokenOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = securityKey,
                    ClockSkew = TimeSpan.Zero
                };

                tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

