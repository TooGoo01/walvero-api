using Loyalty.Business.Dtos.Get.Users;
using Loyalty.Business.Resources;
using Loyalty.Business.Dtos.Post.Users;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Users;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.Infrastructure.Utilities.Security.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Loyalty.Business.Services.Implementations.Users
{
    public class GuestAuthService : IGuestAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITokenHelper _tokenHelper;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<GuestAuthService> _logger;

        public GuestAuthService(
            UserManager<ApplicationUser> userManager,
            ITokenHelper tokenHelper,
            LoyaltyWalletDbContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            ILogger<GuestAuthService> logger)
        {
            _userManager = userManager;
            _tokenHelper = tokenHelper;
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<ServiceResult> GuestLoginAsync(GuestLoginDto dto)
        {
            try
            {
                // 1) Check if there's an existing non-revoked DeviceSession for this deviceId
                var existingSession = await _dbContext.DeviceSessions
                    .Include(ds => ds.User)
                        .ThenInclude(u => u.Tenant)
                            .ThenInclude(t => t.LoyaltyPrograms)
                                .ThenInclude(lp => lp.CardTemplate)
                    .FirstOrDefaultAsync(ds => ds.DeviceId == dto.DeviceId && !ds.IsRevoked);

                if (existingSession != null && existingSession.User != null)
                {
                    var user = existingSession.User;

                    // Check if user is blocked/invalid
                    if (user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
                    {
                        // User is locked out, revoke session and create new guest
                        existingSession.IsRevoked = true;
                        _dbContext.DeviceSessions.Update(existingSession);
                        await _dbContext.SaveChangesAsync();
                    }
                    else
                    {
                        // Update LastSeenAt
                        existingSession.LastSeenAt = DateTime.UtcNow;
                        _dbContext.DeviceSessions.Update(existingSession);
                        await _dbContext.SaveChangesAsync();

                        // Generate JWT for existing user
                        var claims = await BuildClaimsAsync(user);
                        var token = _tokenHelper.CreateToken(user, claims);

                        // Save refresh token
                        await SaveRefreshTokenAsync(user.Id, token.RefreshToken!, token.RefreshTokenExpiration!.Value);

                        // Get user roles
                        var roles = await _userManager.GetRolesAsync(user);
                        List<string>? roleList = roles.ToList();

                        // Get ProgramId and GoogleLogoUrl from Tenant's LoyaltyPrograms if available
                        int? programId = null;
                        string? googleLogoUrl = null;
                        if (user.TenantId != null && user.Tenant?.LoyaltyPrograms != null)
                        {
                            var firstProgram = user.Tenant.LoyaltyPrograms.FirstOrDefault(lp => lp.IsActive && lp.CardTemplate != null);
                            if (firstProgram != null)
                            {
                                programId = firstProgram.Id;
                                googleLogoUrl = firstProgram.CardTemplate?.GoogleLogoUrl;
                            }
                        }

                        var response = new GuestLoginResponseDto
                        {
                            Id = user.Id,
                            TenantId = user.TenantId,
                            ProgramId = programId,
                            UserName = user.UserName,
                            FirstName = user.Name ?? "",
                            LastName = user.Surname ?? "",
                            Email = user.Email,
                            Phone = user.PhoneNumber,
                            BirthDate = user.DateOfBirth,
                            Token = token.Token,
                            Expiration = token.Expiration,
                            RefreshToken = token.RefreshToken,
                            RefreshTokenExpiration = token.RefreshTokenExpiration,
                            Roles = roleList,
                            IsGuest = user.IsGuest,
                            IsProfileCompleted = user.IsProfileCompleted,
                            GoogleLogoUrl = googleLogoUrl
                        };

                        return new ServiceResult(true, response);
                    }
                }

                // 2) No existing session - create new guest user
                // First create user to get ID, then update username with ID
                var guestUser = new ApplicationUser
                {
                    UserName = "guest-temp", // Temporary, will be updated after save
                    NormalizedUserName = "GUEST-TEMP",
                    PhoneNumber = "guest-temp",
                    Email = "guest-temp@walvero.com",
                    EmailConfirmed = false,
                    PhoneNumberConfirmed = false,
                    IsGuest = true,
                    IsProfileCompleted = false,
                    GuestCreatedAt = DateTime.UtcNow,
                    GuestDeviceId = dto.DeviceId,
                    Name = "Guest",
                    Surname = "User",
                    Position = "Customer",
                    TenantId = null // Guest user hələ tenant seçməyib, tenant-a qoşulanda təyin olunacaq
                };

                var createResult = await _userManager.CreateAsync(guestUser);
                if (!createResult.Succeeded)
                {
                    return new ServiceResult(false, $"Failed to create guest user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                }

                // Update username with 6-digit ID format: guest-{id padded to 6 digits}
                var userIdString = guestUser.Id.ToString().PadLeft(6, '0');
                var finalUserName = $"guest-{userIdString}";
                guestUser.UserName = finalUserName;
                guestUser.NormalizedUserName = finalUserName.ToUpperInvariant();
                guestUser.PhoneNumber = finalUserName;
                guestUser.Email = $"{finalUserName}@walvero.com";
                
                var updateResult = await _userManager.UpdateAsync(guestUser);
                if (!updateResult.Succeeded)
                {
                    return new ServiceResult(false, $"Failed to update guest user name: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
                }

                // Add Customer role
                await _userManager.AddToRoleAsync(guestUser, "Customer");

                // Optionally create Customer entity if required by domain
                // For now, we'll skip this as Customer is created during enrollment

                // Create DeviceSession
                var deviceSession = new DeviceSession
                {
                    ApplicationUserId = guestUser.Id,
                    DeviceId = dto.DeviceId,
                    IsGuest = true,
                    IsRevoked = false,
                    CreatedAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow,
                    RegUserId = guestUser.Id,
                    RegDate = DateTime.UtcNow,
                    IsActive = true
                };

                await _dbContext.DeviceSessions.AddAsync(deviceSession);
                await _dbContext.SaveChangesAsync();

                // Generate JWT
                var guestClaims = await BuildClaimsAsync(guestUser);
                var guestToken = _tokenHelper.CreateToken(guestUser, guestClaims);

                // Save refresh token
                await SaveRefreshTokenAsync(guestUser.Id, guestToken.RefreshToken!, guestToken.RefreshTokenExpiration!.Value);

                // Get user roles
                var guestRoles = await _userManager.GetRolesAsync(guestUser);
                List<string>? guestRoleList = guestRoles.ToList();

                // Get ProgramId and GoogleLogoUrl from Tenant's LoyaltyPrograms if available
                int? guestProgramId = null;
                string? guestGoogleLogoUrl = null;
                if (guestUser.TenantId != null && guestUser.Tenant?.LoyaltyPrograms != null)
                {
                    var firstProgram = guestUser.Tenant.LoyaltyPrograms.FirstOrDefault(lp => lp.IsActive && lp.CardTemplate != null);
                    if (firstProgram != null)
                    {
                        guestProgramId = firstProgram.Id;
                        guestGoogleLogoUrl = firstProgram.CardTemplate?.GoogleLogoUrl;
                    }
                }

                var guestResponse = new GuestLoginResponseDto
                {
                    Id = guestUser.Id,
                    TenantId = guestUser.TenantId,
                    ProgramId = guestProgramId,
                    UserName = guestUser.UserName,
                    FirstName = guestUser.Name ?? "",
                    LastName = guestUser.Surname ?? "",
                    Email = guestUser.Email,
                    Phone = guestUser.PhoneNumber,
                    BirthDate = guestUser.DateOfBirth,
                    Token = guestToken.Token,
                    Expiration = guestToken.Expiration,
                    RefreshToken = guestToken.RefreshToken,
                    RefreshTokenExpiration = guestToken.RefreshTokenExpiration,
                    Roles = guestRoleList,
                    IsGuest = true,
                    IsProfileCompleted = false,
                    GoogleLogoUrl = guestGoogleLogoUrl
                };

                return new ServiceResult(true, guestResponse);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Error in guest login: {ex.Message}");
            }
        }

        public async Task<ServiceResult> CompleteProfileAsync(CompleteProfileDto dto)
        {
            try
            {
                // Get current user from JWT
                var userIdStr = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                {
                    return new ServiceResult(false, AppMessages.Get("NotAuthenticated"));
                }

                var user = await _dbContext.Users
                    .Include(u => u.Tenant)
                        .ThenInclude(t => t.LoyaltyPrograms)
                            .ThenInclude(lp => lp.CardTemplate)
                    .FirstOrDefaultAsync(u => u.Id == userId);
                
                if (user == null)
                {
                    return new ServiceResult(false, AppMessages.Get("UserNotFound"));
                }

                // Ensure user is guest or profile not completed
                if (!user.IsGuest && user.IsProfileCompleted)
                {
                    // User is already a full user, just update profile normally
                    user.Name = dto.FullName.Split(' ').FirstOrDefault() ?? dto.FullName;
                    user.Surname = dto.FullName.Split(' ').Length > 1 
                        ? string.Join(" ", dto.FullName.Split(' ').Skip(1)) 
                        : "";
                    if (!string.IsNullOrEmpty(dto.Email))
                    {
                        user.Email = dto.Email;
                    }

                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        return new ServiceResult(false, $"Failed to update profile: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
                    }

                    return new ServiceResult(true, "Profile updated successfully.");
                }

                // Validate phone number - ensure no other NON-GUEST user uses it
                var existingUser = await _userManager.FindByNameAsync(dto.PhoneNumber);
                if (existingUser != null && existingUser.Id != user.Id && !existingUser.IsGuest)
                {
                    return new ServiceResult(false, AppMessages.Get("PhoneAlreadyExists"));
                }

                // IMPORTANT: Update existing ApplicationUser, do NOT create new one
                var oldUserName = user.UserName;
                user.UserName = dto.PhoneNumber;
                user.NormalizedUserName = dto.PhoneNumber.ToUpperInvariant();
                user.PhoneNumber = dto.PhoneNumber;
                user.PhoneNumberConfirmed = false; // Will be confirmed via OTP if needed
                
                if (!string.IsNullOrEmpty(dto.Email))
                {
                    user.Email = dto.Email;
                }

                // Parse full name
                var nameParts = dto.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                user.Name = nameParts.Length > 0 ? nameParts[0] : dto.FullName;
                user.Surname = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "";

                // Update guest status
                user.IsGuest = false;
                user.IsProfileCompleted = true;
                user.ProfileCompletedAt = DateTime.UtcNow;
                user.GuestDeviceId = null; // Clear guest device ID

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return new ServiceResult(false, $"Failed to complete profile: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }

                // Update all DeviceSession rows for this user
                var sessions = await _dbContext.DeviceSessions
                    .Where(ds => ds.ApplicationUserId == user.Id)
                    .ToListAsync();

                foreach (var session in sessions)
                {
                    session.IsGuest = false;
                }
                _dbContext.DeviceSessions.UpdateRange(sessions);
                await _dbContext.SaveChangesAsync();

                // Update Customer entities if they exist (already linked)
                var customers = await _dbContext.Customers
                    .Where(c => c.applicationUserId == user.Id)
                    .ToListAsync();

                foreach (var customer in customers)
                {
                    customer.FirstName = user.Name;
                    customer.LastName = user.Surname;
                    customer.PhoneNumber = dto.PhoneNumber;
                    if (!string.IsNullOrEmpty(dto.Email))
                    {
                        customer.Email = dto.Email;
                    }
                }
                _dbContext.Customers.UpdateRange(customers);

                // Link orphan Customers that have the same phone but no applicationUserId
                var orphanCustomers = await _dbContext.Customers
                    .Where(c => c.PhoneNumber == dto.PhoneNumber
                        && c.applicationUserId == null
                        && c.IsActive)
                    .ToListAsync();

                foreach (var orphan in orphanCustomers)
                {
                    orphan.applicationUserId = user.Id;
                }
                if (orphanCustomers.Count > 0)
                    _dbContext.Customers.UpdateRange(orphanCustomers);

                await _dbContext.SaveChangesAsync();

                // Generate new JWT with updated claims
                var claims = await BuildClaimsAsync(user);
                var token = _tokenHelper.CreateToken(user, claims);

                // Save refresh token
                await SaveRefreshTokenAsync(user.Id, token.RefreshToken!, token.RefreshTokenExpiration!.Value);

                // Get user roles
                var roles = await _userManager.GetRolesAsync(user);
                List<string>? roleList = roles.ToList();

                // Get ProgramId and GoogleLogoUrl from Tenant's LoyaltyPrograms if available
                int? programId = null;
                string? googleLogoUrl = null;
                if (user.TenantId != null && user.Tenant?.LoyaltyPrograms != null)
                {
                    var firstProgram = user.Tenant.LoyaltyPrograms.FirstOrDefault(lp => lp.IsActive && lp.CardTemplate != null);
                    if (firstProgram != null)
                    {
                        programId = firstProgram.Id;
                        googleLogoUrl = firstProgram.CardTemplate?.GoogleLogoUrl;
                    }
                }

                var response = new GuestLoginResponseDto
                {
                    Id = user.Id,
                    TenantId = user.TenantId,
                    ProgramId = programId,
                    UserName = user.UserName,
                    FirstName = user.Name ?? "",
                    LastName = user.Surname ?? "",
                    Email = user.Email,
                    Phone = user.PhoneNumber,
                    BirthDate = user.DateOfBirth,
                    Token = token.Token,
                    Expiration = token.Expiration,
                    RefreshToken = token.RefreshToken,
                    RefreshTokenExpiration = token.RefreshTokenExpiration,
                    Roles = roleList,
                    IsGuest = false,
                    IsProfileCompleted = true,
                    GoogleLogoUrl = googleLogoUrl
                };

                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Error completing profile: {ex.Message}");
            }
        }

        public async Task<ServiceResult> GetProfileStatusAsync()
        {
            try
            {
                var userIdStr = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                {
                    return new ServiceResult(false, AppMessages.Get("NotAuthenticated"));
                }

                var user = await _userManager.FindByIdAsync(userIdStr);
                if (user == null)
                {
                    return new ServiceResult(false, AppMessages.Get("UserNotFound"));
                }

                var status = new ProfileStatusDto
                {
                    IsGuest = user.IsGuest,
                    IsProfileCompleted = user.IsProfileCompleted,
                    RequiresProfileCompletion = user.IsGuest || !user.IsProfileCompleted
                };

                return new ServiceResult(true, status);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Error getting profile status: {ex.Message}");
            }
        }

        public async Task<ServiceResult> RefreshTokenAsync(RefreshTokenDto dto)
        {
            try
            {
                // Find refresh token
                var refreshToken = await _dbContext.RefreshTokens
                    .Include(rt => rt.User)
                    .ThenInclude(u => u.Tenant)
                    .ThenInclude(t => t.LoyaltyPrograms)
                        .ThenInclude(lp => lp.CardTemplate)
                    .Include(rt => rt.User)
                    .ThenInclude(u => u.Branch)
                    .FirstOrDefaultAsync(rt => rt.Token == dto.RefreshToken);

                if (refreshToken == null)
                {
                    return new ServiceResult(false, AppMessages.Get("NotAuthenticated"));
                }

                if (!refreshToken.IsActive)
                {
                    return new ServiceResult(false, refreshToken.IsRevoked ? "Refresh token has been revoked." : "Refresh token has expired.");
                }

                var user = refreshToken.User;
                if (user == null)
                {
                    return new ServiceResult(false, AppMessages.Get("UserNotFound"));
                }

                // Revoke old refresh token
                refreshToken.RevokedAt = DateTime.UtcNow;
                refreshToken.ReasonRevoked = "Replaced by new token";
                refreshToken.RevokedByIp = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
                _dbContext.RefreshTokens.Update(refreshToken);

                // Generate new tokens
                var claims = await BuildClaimsAsync(user);
                var newToken = _tokenHelper.CreateToken(user, claims);

                // Save new refresh token
                await SaveRefreshTokenAsync(user.Id, newToken.RefreshToken!, newToken.RefreshTokenExpiration!.Value, refreshToken.Token);

                await _dbContext.SaveChangesAsync();

                // Get user roles
                var roles = await _userManager.GetRolesAsync(user);
                List<string>? roleList = roles.ToList();

                // Get ProgramId and GoogleLogoUrl from Tenant's LoyaltyPrograms if available
                int? programId = null;
                string? googleLogoUrl = null;
                if (user.TenantId != null && user.Tenant?.LoyaltyPrograms != null)
                {
                    var firstProgram = user.Tenant.LoyaltyPrograms.FirstOrDefault(lp => lp.IsActive && lp.CardTemplate != null);
                    if (firstProgram != null)
                    {
                        programId = firstProgram.Id;
                        googleLogoUrl = firstProgram.CardTemplate?.GoogleLogoUrl;
                    }
                }

                var response = new GuestLoginResponseDto
                {
                    Id = user.Id,
                    TenantId = user.TenantId,
                    ProgramId = programId,
                    BranchId = user.BranchId,
                    BranchName = user.Branch?.Name,
                    UserName = user.UserName,
                    FirstName = user.Name ?? "",
                    LastName = user.Surname ?? "",
                    Email = user.Email,
                    Phone = user.PhoneNumber,
                    BirthDate = user.DateOfBirth,
                    Token = newToken.Token,
                    Expiration = newToken.Expiration,
                    RefreshToken = newToken.RefreshToken,
                    RefreshTokenExpiration = newToken.RefreshTokenExpiration,
                    Roles = roleList,
                    IsGuest = user.IsGuest,
                    IsProfileCompleted = user.IsProfileCompleted,
                    GoogleLogoUrl = googleLogoUrl
                };

                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while refreshing token");
                return new ServiceResult(false, $"Error refreshing token: {ex.Message}");
            }
        }

        private async Task SaveRefreshTokenAsync(int userId, string refreshToken, DateTime expiresAt, string? replacedByToken = null)
        {
            var refreshTokenEntity = new RefreshToken
            {
                ApplicationUserId = userId,
                Token = refreshToken,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow,
                ReplacedByToken = replacedByToken,
                RegUserId = userId,
                RegDate = DateTime.UtcNow
                // IsActive is computed property - automatically true if not revoked and not expired
            };

            await _dbContext.RefreshTokens.AddAsync(refreshTokenEntity);
            await _dbContext.SaveChangesAsync();
        }

        private async Task<List<Claim>> BuildClaimsAsync(ApplicationUser user)
        {
            var claims = new List<Claim>();

            // Add roles
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Add guest status claims
            claims.Add(new Claim("is_guest", user.IsGuest.ToString().ToLower()));
            claims.Add(new Claim("is_profile_completed", user.IsProfileCompleted.ToString().ToLower()));

            // Add tenant_id if exists
            if (user.TenantId.HasValue)
            {
                claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));
            }

            return claims;
        }
    }
}

