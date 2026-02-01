using Loyalty.Business.Dtos.Get.Main;
using Loyalty.DataAccess.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class HealthController : ControllerBase
    {
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HealthController> _logger;
        private static readonly DateTime _startTime = DateTime.UtcNow;

        public HealthController(
            LoyaltyWalletDbContext dbContext,
            IConfiguration configuration,
            ILogger<HealthController> logger)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Comprehensive health check endpoint
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(HealthCheckResponseDto), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Check(CancellationToken ct = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new HealthCheckResponseDto
            {
                Timestamp = DateTime.UtcNow,
                Uptime = DateTime.UtcNow - _startTime,
                Version = GetVersion()
            };

            try
            {
                // Check database connectivity
                response.Details.Database = await CheckDatabaseAsync(ct);

                // Check authentication configuration
                response.Details.Authentication = CheckAuthentication();

                // Check memory
                response.Details.Memory = GetMemoryStatus();

                // Determine overall health
                response.Success = response.Details.Database.IsHealthy && 
                                  response.Details.Authentication.IsHealthy;
                response.Status = response.Success ? "Healthy" : "Unhealthy";

                stopwatch.Stop();
                response.Details.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

                var statusCode = response.Success ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
                return StatusCode((int)statusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                stopwatch.Stop();
                response.Success = false;
                response.Status = "Error";
                response.Details.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                response.Details.Database = new ComponentStatus
                {
                    IsHealthy = false,
                    Status = "Error",
                    Message = ex.Message
                };

                return StatusCode((int)HttpStatusCode.ServiceUnavailable, response);
            }
        }

        /// <summary>
        /// Quick health check - just returns OK if service is running
        /// </summary>
        [HttpGet("ping")]
        [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
        public IActionResult Ping()
        {
            return Ok(new { status = "OK", timestamp = DateTime.UtcNow });
        }

        private async Task<ComponentStatus> CheckDatabaseAsync(CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Try to connect to database
                var canConnect = await _dbContext.Database.CanConnectAsync(ct);
                
                if (canConnect)
                {
                    // Execute a simple query to ensure database is responsive
                    // Using a simple count query that's fast and doesn't require specific tables
                    await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken: ct);
                    stopwatch.Stop();

                    return new ComponentStatus
                    {
                        IsHealthy = true,
                        Status = "Connected",
                        Message = "Database is accessible and responsive",
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }
                else
                {
                    stopwatch.Stop();
                    return new ComponentStatus
                    {
                        IsHealthy = false,
                        Status = "Disconnected",
                        Message = "Cannot connect to database",
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogWarning(ex, "Database health check failed");
                return new ComponentStatus
                {
                    IsHealthy = false,
                    Status = "Error",
                    Message = ex.Message,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private ComponentStatus CheckAuthentication()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var tokenOptions = _configuration.GetSection("TokenOptions");
                var securityKey = tokenOptions["SecurityKey"];
                var issuer = tokenOptions["Issuer"];
                var audience = tokenOptions["Audience"];

                var isValid = !string.IsNullOrEmpty(securityKey) &&
                             !string.IsNullOrEmpty(issuer) &&
                             !string.IsNullOrEmpty(audience);

                stopwatch.Stop();

                return new ComponentStatus
                {
                    IsHealthy = isValid,
                    Status = isValid ? "Configured" : "Misconfigured",
                    Message = isValid 
                        ? "JWT authentication is properly configured" 
                        : "JWT authentication configuration is missing or invalid",
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogWarning(ex, "Authentication health check failed");
                return new ComponentStatus
                {
                    IsHealthy = false,
                    Status = "Error",
                    Message = ex.Message,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        private MemoryStatus GetMemoryStatus()
        {
            var workingSet = GC.GetTotalMemory(false);
            var process = Process.GetCurrentProcess();
            var workingSetBytes = process.WorkingSet64;

            return new MemoryStatus
            {
                WorkingSetBytes = workingSetBytes,
                GCMemoryBytes = workingSet,
                WorkingSetMB = $"{workingSetBytes / 1024.0 / 1024.0:F2}",
                GCMemoryMB = $"{workingSet / 1024.0 / 1024.0:F2}"
            };
        }

        private string GetVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                var informationalVersion = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;

                return informationalVersion ?? version?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}

