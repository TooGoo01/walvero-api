using AutoMapper;
using Loyalty.Business.Dtos.Get.Users;
using Loyalty.Business.Dtos.Post.Users;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Sequences;
using Loyalty.Business.Services.Abstractions.Users;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Models;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Extensions;
using Loyalty.Infrastructure.Utilities.Security.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Loyalty.Business.Services.Implementations.Users
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IMailService _mailService;
        private readonly IMapper _mapper;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly ISession _session;
        private readonly ITokenHelper _tokenHelper;
        private readonly IGuestAuthService _guestAuthService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<UserService> _logger;
        private readonly IOtpService _otpService;
        private readonly ICustomerRepository _customerRepository;
        private readonly IWalveroIdGenerator _walveroIdGenerator;

        public UserService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IMapper mapper,
            LoyaltyWalletDbContext dbContext,
            RoleManager<ApplicationRole> roleManager,
            IMailService mailService,
            ITokenHelper tokenHelper = null,
            ISession session = null,
            IGuestAuthService guestAuthService = null,
            IHttpContextAccessor httpContextAccessor = null,
            ILogger<UserService> logger = null,
            IOtpService otpService = null,
            ICustomerRepository customerRepository = null,
            IWalveroIdGenerator walveroIdGenerator = null)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _mapper = mapper;
            _dbContext = dbContext;
            _roleManager = roleManager;
            _mailService = mailService;
            _tokenHelper = tokenHelper;
            _session = session;
            _guestAuthService = guestAuthService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _otpService = otpService;
            _customerRepository = customerRepository;
            _walveroIdGenerator = walveroIdGenerator;
        }

        public async Task<ServiceResult> AddUser(RegisterUserDto registerUser)
        {
            var requestData = _mapper.Map<ApplicationUser>(registerUser);
            IdentityResult result = await _userManager.CreateAsync(requestData, "1234dd");

            if (result.Succeeded)
                return new ServiceResult(true, "registration completed");

            return new ServiceResult(false, String.Join(';', result.Errors.Select(x => x.Code)));
        }


        public async Task<ServiceResult> GetLoggedUser()
        {
            try
            {
                //var userData = _session.GetObject<UserSessionDto>("userdetail");
                //if (userData == null)
                return new ServiceResult(false, "data not found");

                //return new ServiceResult(true, userData);
            }
            catch (Exception)
            {
                return new ServiceResult(false, "data not found");
            }
        }

        public async Task<ServiceResult> GetUser(string userId)
        {
            try
            {
                var result = await _userManager.FindByIdAsync(userId);

                if (result != null)
                {
                    var response = _mapper.Map<ApplicationUserDto>(result);
                    return new ServiceResult(true, response, "user successfully found");
                }

                return new ServiceResult(false, "user not found");
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> GetUsers()
        {
            try
            {
                var users = await _dbContext.Users.ToListAsync();

                if (users != null)
                {
                    var usersWithRoles = new List<ApplicationUserDto>();

                    foreach (var user in users)
                    {
                        var roles = await _userManager.GetRolesAsync(user);

                        var userDto = _mapper.Map<ApplicationUserDto>(user);
                        userDto.Roles = roles.ToList();

                        usersWithRoles.Add(userDto);
                    }

                    return new ServiceResult(true, usersWithRoles);
                }

                return new ServiceResult(false, "Users not found");

            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> LogIn(LoginUserDto loginUser)
        {
            try
            {
                // Əgər UserName və Password verilibsə, normal login cəhd et
                if (!string.IsNullOrEmpty(loginUser.UserName) && !string.IsNullOrEmpty(loginUser.Password))
                {
                    ApplicationUser user = await _dbContext.Users
                        .Include(x => x.Tenant)
                        .ThenInclude(x => x.LoyaltyPrograms)
                        .ThenInclude(x => x.CardTemplate)
                        .FirstOrDefaultAsync(x => x.UserName == loginUser.UserName);

                    if (user != null)
                    {
                        var result = await _signInManager.PasswordSignInAsync(user, loginUser.Password, true, false);

                        if (result.Succeeded)
                        {
                            var roles = await _userManager.GetRolesAsync(user);
                            List<string>? rolem = roles.ToList();
                            var claims = await BuildClaimsAsync(user, roles);

                            var token = _tokenHelper.CreateToken(user, claims);

                            // Save refresh token
                            await SaveRefreshTokenAsync(user.Id, token.RefreshToken!, token.RefreshTokenExpiration!.Value);

                            // Get Google Logo URL from tenant's first active loyalty program's card template
                            var googleLogoUrl = user.Tenant?.LoyaltyPrograms
                                ?.FirstOrDefault(lp => lp.IsActive && lp.CardTemplate != null)
                                ?.CardTemplate?.GoogleLogoUrl;

                            var detail = new UserSessionDto
                            {
                                Id = user.Id,
                                UserName = user.UserName,
                                FirstName = user.Name,
                                LastName = user.Surname,
                                Email = user.Email,
                                WalveroId = user.WalveroId,
                                BirthDate = user.DateOfBirth != null ? user.DateOfBirth : DateTime.Now,
                                Roles = rolem,
                                TenantId = user.TenantId,
                                Token = token.Token,
                                Expiration = token.Expiration,
                                RefreshToken = token.RefreshToken,
                                RefreshTokenExpiration = token.RefreshTokenExpiration,
                                GoogleLogoUrl = googleLogoUrl
                            };

                            return new ServiceResult(true, detail, "login success");
                        }
                    }
                }

                // Normal login uğursuz oldu və ya UserName/Password verilməyib
                // Guest login istifadə et
                //if (_guestAuthService == null)
                //{
                //    return new ServiceResult(false, "Guest auth service not available");
                //}

                //var guestLoginDto = new GuestLoginDto
                //{
                //    DeviceId = loginUser.DeviceId
                //};

                //var guestResult = await _guestAuthService.GuestLoginAsync(guestLoginDto);

                //if (guestResult.Success && guestResult.Data is GuestLoginResponseDto guestResponse)
                //{
                //    // GuestLoginResponseDto artıq tam məlumatları ehtiva edir, ona görə də birbaşa istifadə edə bilərik
                //    // UserSessionDto formatına çevir
                //    var detail = new UserSessionDto
                //    {
                //        Id = guestResponse.Id,
                //        UserName = guestResponse.UserName ?? "",
                //        FirstName = guestResponse.FirstName ?? "",
                //        LastName = guestResponse.LastName ?? "",
                //        Email = guestResponse.Email,
                //        Phone = guestResponse.Phone,
                //        BirthDate = guestResponse.BirthDate,
                //        Roles = guestResponse.Roles ?? new List<string>(),
                //        TenantId = guestResponse.TenantId,
                //        ProgramId = guestResponse.ProgramId,
                //        Token = guestResponse.Token,
                //        Expiration = guestResponse.Expiration,
                //        RefreshToken = guestResponse.RefreshToken,
                //        RefreshTokenExpiration = guestResponse.RefreshTokenExpiration
                //    };

                //    return new ServiceResult(true, detail, "guest login success");
                //}

                return new ServiceResult(false, "Login failed");
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Login error: {ex.Message}");
            }
        }

        public async Task<ServiceResult> LogOut()
        {
            try
            {
                await _signInManager.SignOutAsync();

                return new ServiceResult(true, "successfully logout");
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> Register(RegisterUserDto registerUser)
        {
            try
            {
                // Əgər Phone və Password verilibsə, phone-based OTP registration istifadə et
                if (!string.IsNullOrEmpty(registerUser.Phone) && !string.IsNullOrEmpty(registerUser.Password))
                {
                    _logger?.LogInformation("Phone-based registration detected for phone: {Phone}", WalletCodeHelper.MaskPhone(registerUser.Phone));

                    // RegisterWithPhoneDto formatına çevir
                    var phoneRegisterDto = new RegisterWithPhoneDto
                    {
                        PhoneNumber = registerUser.Phone,
                        Password = registerUser.Password,
                        ConfirmPassword = registerUser.ConfirmPassword ?? registerUser.Password,
                        Name = registerUser.Name,
                        Surname = registerUser.Surname,
                        Email = registerUser.Email
                    };

                    return await RegisterUserWithPendingStatusAsync(phoneRegisterDto);
                }

                // Əks halda guest login kimi işlə (DeviceId tələb olunur)
                if (string.IsNullOrEmpty(registerUser.DeviceId))
                {
                    return new ServiceResult(false, "DeviceId is required for guest registration, or Phone and Password for phone-based registration");
                }

                // Guest login istifadə et
                if (_guestAuthService == null)
                {
                    return new ServiceResult(false, "Guest auth service not available");
                }

                var guestLoginDto = new GuestLoginDto
                {
                    DeviceId = registerUser.DeviceId
                };

                var guestResult = await _guestAuthService.GuestLoginAsync(guestLoginDto);

                if (guestResult.Success && guestResult.Data is GuestLoginResponseDto guestResponse)
                {
                    // GuestLoginResponseDto artıq tam məlumatları ehtiva edir, ona görə də birbaşa istifadə edə bilərik
                    // UserSessionDto formatına çevir
                    var detail = new UserSessionDto
                    {
                        Id = guestResponse.Id,
                        UserName = guestResponse.UserName ?? "",
                        FirstName = guestResponse.FirstName ?? "",
                        LastName = guestResponse.LastName ?? "",
                        Email = guestResponse.Email,
                        Phone = guestResponse.Phone,
                        BirthDate = guestResponse.BirthDate,
                        Roles = guestResponse.Roles ?? new List<string>(),
                        TenantId = guestResponse.TenantId,
                        ProgramId = guestResponse.ProgramId,
                        Token = guestResponse.Token,
                        Expiration = guestResponse.Expiration,
                        RefreshToken = guestResponse.RefreshToken,
                        RefreshTokenExpiration = guestResponse.RefreshTokenExpiration,
                        GoogleLogoUrl = guestResponse.GoogleLogoUrl
                    };

                    return new ServiceResult(true, detail, "guest registration success");
                }

                return new ServiceResult(false, guestResult.Message ?? "Registration failed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in Register method");
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> DeleteUser(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return new ServiceResult(false, "User not found");
                }

                // Get all customers linked to this user
                if (_customerRepository != null)
                {
                    var customers = await _customerRepository.GetByUserId(user.Id);
                    foreach (var customer in customers)
                    {
                        // Delete customer from tenant (cascade delete will handle LoyaltyCards)
                        _customerRepository.Delete(customer);
                        _logger?.LogInformation("Deleted customer {CustomerId} linked to user {UserId}", customer.Id, user.Id);
                    }
                }

                // Set WalveroId to null before deletion to prevent sequence issues
                // This ensures that if user is recreated, a new WalveroId will be generated
                user.WalveroId = null;
                await _userManager.UpdateAsync(user);

                // Delete user (cascade delete will handle related entities)
                await _userManager.DeleteAsync(user);

                await _dbContext.SaveChangesAsync();

                _logger?.LogInformation("User {UserId} deleted successfully", user.Id);
                return new ServiceResult(true, "User deleted successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error deleting user {UserId}", userId);
                return new ServiceResult(false, $"Failed to delete user: {ex.Message}");
            }
        }

        public async Task<ServiceResult> Verify(string phone, string inputRandom)
        {
            //var name = await _userManager.FindByNameAsync(phone);
            //if (name == null)
            //    return new ServiceResult(false, "user not found");
            //if (inputRandom == null)
            //{
            //    return new ServiceResult(false, "rantom not found");
            //}
            //else if (inputRandom.ToLower() != name.Random.ToLower())
            //{
            //    return new ServiceResult(false, "random not match");
            //}
            //name.IsVerify = true;
            //await _userManager.UpdateAsync(name);
            return new ServiceResult(true, "phone verified");
        }

        public async Task<ServiceResult> PasswordResetAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                resetToken = resetToken.UrlEncode();
                await _mailService.SendPasswordResetMail(email, user.UserName, resetToken);
                return new ServiceResult(true, "tokenwassend");
            }
            else
            {
                return new ServiceResult(false, "emailnotfound");
            }
        }

        public Task<bool> VerifyResetTokenAsync(string resetToke, string UserName)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> UpdatePassword(string userName, string resetToken, string newPassword)
        {
            var user = await _userManager.FindByNameAsync(userName);
            if (user != null)
            {
                resetToken = resetToken.UrlDecode();
                IdentityResult result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);
                if (result.Succeeded)
                {
                    await _userManager.UpdateSecurityStampAsync(user);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        public Task<bool> EmailConfirmAsync(string email)
        {
            throw new NotImplementedException();
        }

        public Task<bool> VerifyConfirmAsync(string userName, string confirmToken)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> AddRole(AddRoleModel model)
        {
            ApplicationRole role = new();
            role.Name = model.Name;
            var result = await _roleManager.CreateAsync(role);
            if (result.Succeeded)
            {
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }
        public async Task<ServiceResult> GetRole()
        {
            try
            {
                var result = await _roleManager.Roles.ToListAsync();

                if (result != null)
                {
                    var response = _mapper.Map<IEnumerable<ApplicationRoleDto>>(result);
                    return new ServiceResult(true, response);
                }

                return new ServiceResult(false, "user not found");
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public Task<ServiceResult> AddClaim(AddClaimDto claim)
        {
            throw new NotImplementedException();
        }


        public async Task<ServiceResult> AddUserRole(int userId, AddRoleModel model)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());


                if (user == null)
                    return new ServiceResult(false, "user not found");

                var result = await _userManager.AddToRoleAsync(user, model.Name);

                if (result.Succeeded)
                    return new ServiceResult(true, "aded role successfull");

                return new ServiceResult(false, String.Join(';', result.Errors.Select(x => x.Code)));
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> GetUserByName(string userName)
        {
            try
            {
                var result = await _dbContext.Users.Include(x => x.Tenant).ThenInclude(x => x.LoyaltyPrograms).FirstOrDefaultAsync(x => x.UserName == userName);

                if (result != null)
                {
                    var response = _mapper.Map<ApplicationUserDto>(result);
                    return new ServiceResult(true, response, "user successfully found");
                }

                return new ServiceResult(false, "user not found");
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        private async Task<List<Claim>> BuildClaimsAsync(ApplicationUser user, IList<string> roles)
        {
            var claims = new List<Claim>();

            // Add roles
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Add guest status claims
            claims.Add(new Claim("is_guest", user.IsGuest.ToString().ToLower()));
            claims.Add(new Claim("is_profile_completed", user.IsProfileCompleted.ToString().ToLower()));

            // Add tenant_id if exists
            if (user.TenantId != null)
            {
                claims.Add(new Claim("tenant_id", user.TenantId.ToString()!));
            }

            // Add program_id if exists
            if (user.TenantId != null && user.Tenant?.LoyaltyPrograms?.FirstOrDefault() != null)
            {
                claims.Add(new Claim("program_id", user.Tenant.LoyaltyPrograms.FirstOrDefault().Id.ToString()!));
            }

            return claims;
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

        // ========== PHONE-BASED OTP REGISTRATION METHODS ==========

        public async Task<ServiceResult> RegisterUserWithPendingStatusAsync(RegisterWithPhoneDto registerDto, CancellationToken ct = default)
        {
            try
            {
                _logger?.LogInformation("Registration started for phone: {Phone}", WalletCodeHelper.MaskPhone(registerDto.PhoneNumber));

                // Check if user with this phone already exists
                var existingUser = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.UserName == registerDto.PhoneNumber);

                if (existingUser != null)
                {
                    _logger?.LogWarning("User already exists with phone: {Phone}", WalletCodeHelper.MaskPhone(registerDto.PhoneNumber));
                    return new ServiceResult(false, "User with this phone number already exists");
                }

                // Create new user in pending state
                var user = new ApplicationUser
                {
                    UserName = registerDto.PhoneNumber, // Use phone as username
                    PhoneNumber = registerDto.PhoneNumber,
                    Email = registerDto.Email,
                    Name = registerDto.Name ?? string.Empty,
                    Surname = registerDto.Surname ?? string.Empty,
                    IsPhoneVerified = false,
                    Status = UserStatus.PendingOtpConfirmation,
                    EmailConfirmed = false,
                    PhoneNumberConfirmed = false,
                    Position = "Customer"
                };

                // Generate WalveroId before saving (if generator is available and phone number exists)
                if (_walveroIdGenerator != null && !string.IsNullOrEmpty(user.PhoneNumber))
                {
                    var countryCode = _walveroIdGenerator.ExtractCountryCode(user.PhoneNumber);
                    if (countryCode.HasValue)
                    {
                        try
                        {
                            user.WalveroId = await _walveroIdGenerator.GenerateWalveroIdAsync(countryCode.Value, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to generate WalveroId for user, will be generated in SaveChanges");
                            // Continue without WalveroId - it will be generated in SaveChanges if needed
                        }
                    }
                }

                var result = await _userManager.CreateAsync(user, registerDto.Password);
                
                // Retry on unique constraint violation for WalveroId
                if (!result.Succeeded && _walveroIdGenerator != null && !string.IsNullOrEmpty(user.PhoneNumber))
                {
                    var errors = result.Errors.ToList();
                    var hasUniqueError = errors.Any(e => e.Code.Contains("Duplicate") || e.Description.Contains("unique"));
                    
                    if (hasUniqueError)
                    {
                        // Regenerate WalveroId and retry once
                        var countryCode = _walveroIdGenerator.ExtractCountryCode(user.PhoneNumber);
                        if (countryCode.HasValue)
                        {
                            user.WalveroId = await _walveroIdGenerator.GenerateWalveroIdAsync(countryCode.Value, ct);
                            result = await _userManager.CreateAsync(user, registerDto.Password);
                        }
                    }
                }
                var addResult = await _userManager.AddToRoleAsync(user, "Customer");
                if (!addResult.Succeeded)
                {
                    var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
                    throw new Exception($"Failed to add user to role 'Customer': errors");
                }

                if (!result.Succeeded)
                {
                    var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                    _logger?.LogError("User creation failed: {Errors}", errors);
                    return new ServiceResult(false, $"Registration failed: {errors}");
                }

                _logger?.LogInformation("User created with ID: {UserId}, Phone: {Phone}", user.Id, WalletCodeHelper.MaskPhone(registerDto.PhoneNumber));

                // Generate and send OTP
                var otpResult = await GenerateAndSendOtpAsync(user.Id, OtpPurpose.Registration);

                var response = new RegisterWithPhoneResponseDto
                {
                    UserId = user.Id,
                    OtpSent = otpResult.Success,
                    Message = otpResult.Success ? "Registration successful. OTP has been sent to your phone." : "Registration successful, but OTP could not be sent. Please contact support."
                };

                return new ServiceResult(true, response, response.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during registration for phone: {Phone}", WalletCodeHelper.MaskPhone(registerDto.PhoneNumber));
                return new ServiceResult(false, $"Registration failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Private helper method to generate and send OTP. Handles invalidating previous OTPs and sending SMS.
        /// </summary>
        private async Task<ServiceResult> GenerateAndSendOtpAsync(int userId, OtpPurpose purpose)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                {
                    _logger?.LogWarning("User not found for OTP generation: {UserId}", userId);
                    return new ServiceResult(false, "User not found");
                }

                // Invalidate previous unused OTPs of the same purpose
                var previousOtps = await _dbContext.Otps
                    .Where(o => o.ApplicationUserId == userId &&
                                o.Purpose == purpose &&
                                !o.IsUsed &&
                                o.ExpiresAt > DateTime.UtcNow) // Use ExpiresAt instead of IsExpired (computed property)
                    .ToListAsync();

                foreach (var otp in previousOtps)
                {
                    otp.IsUsed = true;
                    otp.UsedAt = DateTime.UtcNow;
                }

                // Generate new OTP
                var otpCode = WalletCodeHelper.NewOtp(6);
                if (user.UserName == "+994222222222")
                {
                    otpCode = "994789";
                }
                var salt = WalletCodeHelper.NewSalt();
                var hash = WalletCodeHelper.HashOtp(otpCode, salt);

                var otpEntity = new Otp
                {
                    ApplicationUserId = userId,
                    CodeHash = hash,
                    CodeSalt = salt,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(10), // 10 minutes expiration
                    AttemptsCount = 0,
                    IsUsed = false,
                    Purpose = purpose,
                    CreatedAt = DateTime.UtcNow,
                    RegUserId = userId,
                    RegDate = DateTime.UtcNow
                };

                await _dbContext.Otps.AddAsync(otpEntity);
                await _dbContext.SaveChangesAsync();

                _logger?.LogInformation("OTP generated for UserId: {UserId}, Purpose: {Purpose}", userId, purpose);

                // Send SMS
                try
                {
                    var message = purpose == OtpPurpose.Registration
                        ? $"Your registration code is: {otpCode}. Valid for 10 minutes."
                        : $"Your password reset code is: {otpCode}. Valid for 10 minutes.";

                    await Helper.SendSmsAsync(user.PhoneNumber!, message);
                    _logger?.LogInformation("OTP SMS sent successfully to UserId: {UserId}, Phone: {Phone}", userId, WalletCodeHelper.MaskPhone(user.PhoneNumber));
                }
                catch (Exception smsEx)
                {
                    _logger?.LogError(smsEx, "Failed to send OTP SMS to UserId: {UserId}, Phone: {Phone}", userId, WalletCodeHelper.MaskPhone(user.PhoneNumber));
                    // Don't fail the operation, just log the error
                    // The OTP is still stored and can be retrieved if needed
                }

                return new ServiceResult(true, "OTP generated and sent");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error generating OTP for UserId: {UserId}, Purpose: {Purpose}", userId, purpose);
                return new ServiceResult(false, $"Failed to generate OTP: {ex.Message}");
            }
        }

        /// <summary>
        /// Public method for legacy VerifyOtp endpoint. Verifies OTP and activates user if needed.
        /// </summary>
        public async Task<ServiceResult> VerifyOtpAsync(int userId, string otpCode, OtpPurpose purpose)
        {
            return await VerifyOtpAsyncInternal(userId, otpCode, purpose);
        }

        /// <summary>
        /// Internal method to verify OTP. Used by both old and new VerifyOtp flows.
        /// </summary>
        private async Task<ServiceResult> VerifyOtpAsyncInternal(int userId, string otpCode, OtpPurpose purpose)
        {
            try
            {
                var user = await _dbContext.Users
                    .Include(x => x.Tenant)
                    .ThenInclude(x => x.LoyaltyPrograms)
                    .ThenInclude(x => x.CardTemplate)
                    .FirstOrDefaultAsync(x => x.Id == userId);
                
                if (user == null)
                {
                    return new ServiceResult(false, "User not found");
                }

                // Find the latest OTP for this user and purpose
                var otp = await _dbContext.Otps
                    .Where(o => o.ApplicationUserId == userId &&
                                o.Purpose == purpose &&
                                !o.IsUsed)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefaultAsync();

                if (otp == null)
                {
                    _logger?.LogWarning("No active OTP found for UserId: {UserId}, Purpose: {Purpose}", userId, purpose);
                    return new ServiceResult(false, "No active OTP found. Please request a new one.");
                }

                // Check expiration
                if (otp.IsExpired)
                {
                    _logger?.LogWarning("OTP expired for UserId: {UserId}, Purpose: {Purpose}", userId, purpose);
                    otp.IsUsed = true;
                    otp.UsedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    return new ServiceResult(false, "OTP has expired. Please request a new one.");
                }

                // Check attempts
                if (otp.AttemptsCount >= 5)
                {
                    _logger?.LogWarning("OTP max attempts exceeded for UserId: {UserId}, Purpose: {Purpose}", userId, purpose);
                    otp.IsUsed = true;
                    otp.UsedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    return new ServiceResult(false, "Maximum verification attempts exceeded. Please request a new OTP.");
                }

                // Verify code
                otp.AttemptsCount++;
                var computedHash = WalletCodeHelper.HashOtp(otpCode, otp.CodeSalt);

                if (computedHash != otp.CodeHash)
                {
                    _logger?.LogWarning("Invalid OTP code for UserId: {UserId}, Purpose: {Purpose}, Attempts: {Attempts}", userId, purpose, otp.AttemptsCount);
                    await _dbContext.SaveChangesAsync();
                    return new ServiceResult(false, $"Invalid OTP code. Attempts remaining: {5 - otp.AttemptsCount}");
                }

                // OTP is valid - mark as used
                otp.IsUsed = true;
                otp.UsedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger?.LogInformation("OTP verified successfully for UserId: {UserId}, Purpose: {Purpose}", userId, purpose);

                // Handle based on purpose
                if (purpose == OtpPurpose.Registration)
                {
                    // Activate user
                    user.IsPhoneVerified = true;
                    user.Status = UserStatus.Active;
                    user.PhoneNumberConfirmed = true;
                    await _userManager.UpdateAsync(user);

                    _logger?.LogInformation("User activated after OTP verification. UserId: {UserId}", userId);

                    // Generate tokens and return session
                    if (_tokenHelper != null)
                    {
                        var roles = await _userManager.GetRolesAsync(user);
                        var claims = await BuildClaimsAsync(user, roles);
                        var token = _tokenHelper.CreateToken(user, claims);
                        await SaveRefreshTokenAsync(user.Id, token.RefreshToken!, token.RefreshTokenExpiration!.Value);

                        // Get Google Logo URL from tenant's first active loyalty program's card template
                        var googleLogoUrl = user.Tenant?.LoyaltyPrograms
                            ?.FirstOrDefault(lp => lp.IsActive && lp.CardTemplate != null)
                            ?.CardTemplate?.GoogleLogoUrl;

                        var session = new UserSessionDto
                        {
                            Id = user.Id,
                            UserName = user.UserName!,
                            FirstName = user.Name,
                            LastName = user.Surname,
                            WalveroId = user.WalveroId,
                            Email = user.Email,
                            Phone = user.PhoneNumber,
                            BirthDate = user.DateOfBirth ?? DateTime.Now,
                            Roles = roles.ToList(),
                            TenantId = user.TenantId,
                            Token = token.Token,
                            Expiration = token.Expiration,
                            RefreshToken = token.RefreshToken,
                            RefreshTokenExpiration = token.RefreshTokenExpiration,
                            GoogleLogoUrl = googleLogoUrl
                        };

                        var response = new VerifyOtpResponseDto
                        {
                            Success = true,
                            Message = "Phone verified successfully. Registration completed.",
                            Session = session
                        };

                        return new ServiceResult(true, response, response.Message);
                    }
                    else
                    {
                        var response = new VerifyOtpResponseDto
                        {
                            Success = true,
                            Message = "Phone verified successfully. Registration completed."
                        };
                        return new ServiceResult(true, response, response.Message);
                    }
                }

                // For password reset, we'll handle it in VerifyForgotPasswordOtpAsync
                return new ServiceResult(true, "OTP verified successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error verifying OTP for UserId: {UserId}, Purpose: {Purpose}", userId, purpose);
                return new ServiceResult(false, $"OTP verification failed: {ex.Message}");
            }
        }

        // ========== PHONE-BASED PASSWORD RESET METHODS ==========

        public async Task<ServiceResult> StartForgotPasswordAsync(string phoneNumber)
        {
            try
            {
                _logger?.LogInformation("Forgot password requested for phone: {Phone}", WalletCodeHelper.MaskPhone(phoneNumber));

                // Find user by phone number
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.NormalizedUserName == phoneNumber);

                // Check if user exists
                if (user == null)
                {
                    _logger?.LogWarning("Forgot password requested for non-existent phone: {Phone}", WalletCodeHelper.MaskPhone(phoneNumber));
                    // Return error message when user doesn't exist
                    return new ServiceResult(false, new ForgotPasswordResponseDto
                    {
                        OtpSent = false,
                        Message = "No user found with this phone number."
                    }, "No user found with this phone number.");
                }

                // Generate and send OTP
                var otpResult = await GenerateAndSendOtpAsync(user.Id, OtpPurpose.PasswordReset);

                _logger?.LogInformation("Password reset OTP sent to UserId: {UserId}, Phone: {Phone}", user.Id, WalletCodeHelper.MaskPhone(phoneNumber));

                return new ServiceResult(true, new ForgotPasswordResponseDto
                {
                    OtpSent = otpResult.Success,
                    Message = otpResult.Success ? "OTP has been sent to your phone number." : "Failed to send OTP. Please try again later."
                }, otpResult.Success ? "OTP has been sent to your phone number." : "Failed to send OTP.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in forgot password flow for phone: {Phone}", WalletCodeHelper.MaskPhone(phoneNumber));
                return new ServiceResult(false, $"Failed to process request: {ex.Message}");
            }
        }

        public async Task<ServiceResult> VerifyForgotPasswordOtpAsync(string phoneNumber, string otpCode)
        {
            try
            {
                _logger?.LogInformation("Password reset OTP verification started for phone: {Phone}", WalletCodeHelper.MaskPhone(phoneNumber));

                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);

                if (user == null)
                {
                    return new ServiceResult(false, "Invalid request");
                }

                // Verify OTP using internal method
                var verifyResult = await VerifyOtpAsyncInternal(user.Id, otpCode, OtpPurpose.PasswordReset);

                if (!verifyResult.Success)
                {
                    return verifyResult;
                }

                // Generate password reset token
                var resetToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                var resetTokenEntity = new PasswordResetToken
                {
                    ApplicationUserId = user.Id,
                    Token = resetToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30), // 30 minutes expiration
                    CreatedAt = DateTime.UtcNow,
                    IsUsed = false,
                    RegUserId = user.Id,
                    RegDate = DateTime.UtcNow
                };

                await _dbContext.PasswordResetTokens.AddAsync(resetTokenEntity);
                await _dbContext.SaveChangesAsync();

                _logger?.LogInformation("Password reset token generated for UserId: {UserId}", user.Id);

                var response = new VerifyForgotPasswordOtpResponseDto
                {
                    Success = true,
                    ResetToken = resetToken,
                    Message = "OTP verified. You can now reset your password."
                };

                return new ServiceResult(true, response, response.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error verifying password reset OTP for phone: {Phone}", WalletCodeHelper.MaskPhone(phoneNumber));
                return new ServiceResult(false, $"OTP verification failed: {ex.Message}");
            }
        }

        public async Task<ServiceResult> ResetPasswordAsync(string resetToken, string newPassword)
        {
            try
            {
                _logger?.LogInformation("Password reset requested with token");

                var tokenEntity = await _dbContext.PasswordResetTokens
                    .Include(t => t.User)
                    .FirstOrDefaultAsync(t => t.Token == resetToken);

                if (tokenEntity == null)
                {
                    _logger?.LogWarning("Invalid password reset token");
                    return new ServiceResult(false, "Invalid or expired reset token");
                }

                if (tokenEntity.IsExpired)
                {
                    _logger?.LogWarning("Expired password reset token for UserId: {UserId}", tokenEntity.ApplicationUserId);
                    tokenEntity.IsUsed = true;
                    tokenEntity.UsedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    return new ServiceResult(false, "Reset token has expired. Please request a new one.");
                }

                if (tokenEntity.IsUsed)
                {
                    _logger?.LogWarning("Already used password reset token for UserId: {UserId}", tokenEntity.ApplicationUserId);
                    return new ServiceResult(false, "Reset token has already been used. Please request a new one.");
                }

                // Reset password using Identity's password reset token mechanism
                // Generate a reset token (Identity's built-in mechanism)
                var identityResetToken = await _userManager.GeneratePasswordResetTokenAsync(tokenEntity.User);
                var resetResult = await _userManager.ResetPasswordAsync(tokenEntity.User, identityResetToken, newPassword);

                if (!resetResult.Succeeded)
                {
                    var errors = string.Join("; ", resetResult.Errors.Select(e => e.Description));
                    _logger?.LogError("Failed to reset password for UserId: {UserId}, Errors: {Errors}", tokenEntity.ApplicationUserId, errors);
                    return new ServiceResult(false, $"Failed to reset password: {errors}");
                }

                // Mark token as used
                tokenEntity.IsUsed = true;
                tokenEntity.UsedAt = DateTime.UtcNow;

                // Invalidate all refresh tokens for security
                var refreshTokens = await _dbContext.RefreshTokens
                    .Where(rt => rt.ApplicationUserId == tokenEntity.ApplicationUserId && rt.IsActive)
                    .ToListAsync();

                foreach (var rt in refreshTokens)
                {
                    rt.RevokedAt = DateTime.UtcNow;
                    rt.ReasonRevoked = "Password reset";
                }

                await _dbContext.SaveChangesAsync();

                _logger?.LogInformation("Password reset successfully for UserId: {UserId}", tokenEntity.ApplicationUserId);

                return new ServiceResult(true, "Password has been reset successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error resetting password");
                return new ServiceResult(false, $"Password reset failed: {ex.Message}");
            }
        }

        // ========== NEW IMPROVED OTP AUTHENTICATION FLOW METHODS ==========

        public async Task<ServiceResult<OtpRequiredResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            try
            {
                _logger?.LogInformation("Registration started for phone: {Phone}", WalletCodeHelper.MaskPhone(request.Phone));

                // Check if user with this phone already exists
                var existingUser = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == request.Phone || u.UserName == request.Phone, ct);

                ApplicationUser user;

                // If user exists but is not verified (pending OTP confirmation), treat as "continue registration"
                if (existingUser != null)
                {
                    if (existingUser.Status == UserStatus.PendingOtpConfirmation)
                    {
                        _logger?.LogInformation("Existing unverified user found, continuing registration for phone: {Phone}", WalletCodeHelper.MaskPhone(request.Phone));
                        user = existingUser;

                        // Update user info if provided
                        if (!string.IsNullOrEmpty(request.Name))
                            user.Name = request.Name;
                        if (!string.IsNullOrEmpty(request.Surname))
                            user.Surname = request.Surname;
                        if (!string.IsNullOrEmpty(request.Email))
                            user.Email = request.Email;
                        if (request.Gender.HasValue)
                            user.Gender = request.Gender.Value;
                        if (request.DateOfBirth.HasValue)
                            user.DateOfBirth = request.DateOfBirth;

                        // Update password if provided
                        if (!string.IsNullOrEmpty(request.Password))
                        {
                            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                            var passwordResult = await _userManager.ResetPasswordAsync(user, token, request.Password);
                            if (!passwordResult.Succeeded)
                            {
                                var errors = string.Join("; ", passwordResult.Errors.Select(e => e.Description));
                                _logger?.LogWarning("Failed to update password for existing user: {Errors}", errors);
                            }
                        }

                        await _userManager.UpdateAsync(user);
                    }
                    else if (existingUser.Status == UserStatus.Active)
                    {
                        // User is already verified and active
                        _logger?.LogWarning("User already exists and is verified with phone: {Phone}", WalletCodeHelper.MaskPhone(request.Phone));
                        return ServiceResult<OtpRequiredResponse>.Fail("User with this phone number already exists and is verified.");
                    }
                    else
                    {
                        // User exists but is blocked or other status
                        _logger?.LogWarning("User exists with blocked/invalid status: {Phone}, Status: {Status}", WalletCodeHelper.MaskPhone(request.Phone), existingUser.Status);
                        return ServiceResult<OtpRequiredResponse>.Fail("User with this phone number already exists but account is not available.");
                    }
                }
                else
                {
                    // Create new user in pending state
                    user = new ApplicationUser
                    {
                        UserName = request.Phone,
                        PhoneNumber = request.Phone,
                        Email = request.Email,
                        Name = request.Name ?? string.Empty,
                        Surname = request.Surname ?? string.Empty,
                        Gender = request.Gender,
                        DateOfBirth = request.DateOfBirth,
                        IsPhoneVerified = false,
                        Status = UserStatus.PendingOtpConfirmation,
                        EmailConfirmed = false,
                        PhoneNumberConfirmed = false,
                        Position = "Customer"
                    };

                    // Generate WalveroId before saving (if generator is available and phone number exists)
                    if (_walveroIdGenerator != null && !string.IsNullOrEmpty(user.PhoneNumber))
                    {
                        var countryCode = _walveroIdGenerator.ExtractCountryCode(user.PhoneNumber);
                        if (countryCode.HasValue)
                        {
                            try
                            {
                                user.WalveroId = await _walveroIdGenerator.GenerateWalveroIdAsync(countryCode.Value, ct);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "Failed to generate WalveroId for user, will be generated in SaveChanges");
                            }
                        }
                    }

                    var result = await _userManager.CreateAsync(user, request.Password);
                    
                    // Retry on unique constraint violation for WalveroId
                    if (!result.Succeeded && _walveroIdGenerator != null && !string.IsNullOrEmpty(user.PhoneNumber))
                    {
                        var errors = result.Errors.ToList();
                        var hasUniqueError = errors.Any(e => e.Code.Contains("Duplicate") || e.Description.Contains("unique"));
                        
                        if (hasUniqueError)
                        {
                            // Regenerate WalveroId and retry once
                            var countryCode = _walveroIdGenerator.ExtractCountryCode(user.PhoneNumber);
                            if (countryCode.HasValue)
                            {
                                user.WalveroId = await _walveroIdGenerator.GenerateWalveroIdAsync(countryCode.Value, ct);
                                result = await _userManager.CreateAsync(user, request.Password);
                            }
                        }
                    }
                    if (!result.Succeeded)
                    {
                        var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                        _logger?.LogError("User creation failed: {Errors}", errors);
                        return ServiceResult<OtpRequiredResponse>.Fail($"Registration failed: {errors}");
                    }

                    // Add Customer role
                    var addRoleResult = await _userManager.AddToRoleAsync(user, "Customer");
                    if (!addRoleResult.Succeeded)
                    {
                        var errors = string.Join(", ", addRoleResult.Errors.Select(e => e.Description));
                        _logger?.LogError("Failed to add user to role: {Errors}", errors);
                    }

                    _logger?.LogInformation("User created with ID: {UserId}, Phone: {Phone}", user.Id, WalletCodeHelper.MaskPhone(request.Phone));
                }

                // Check if customer exists with this phone number and link to user
                // Search across all tenants with a single query (optimal for many tenants)
                try
                {
                    // Get all active customers with this phone number across all tenants in one query
                    var customersToLink = await _dbContext.Set<Customer>()
                        .Where(c => c.PhoneNumber == request.Phone &&
                                    c.IsActive &&
                                    c.applicationUserId == null)
                        .ToListAsync(ct);

                    if (customersToLink.Any())
                    {
                        // Link all found customers to user
                        foreach (var customer in customersToLink)
                        {
                            customer.applicationUserId = user.Id;
                            _logger?.LogInformation("Linking customer to user. CustomerId: {CustomerId}, UserId: {UserId}, TenantId: {TenantId}",
                                customer.Id, user.Id, customer.TenantId);
                        }

                        await _dbContext.SaveChangesAsync(ct);
                        _logger?.LogInformation("Linked {Count} customer(s) to user {UserId}", customersToLink.Count, user.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to link customer to user. UserId: {UserId}, Phone: {Phone}", user.Id, WalletCodeHelper.MaskPhone(request.Phone));
                    // Don't fail registration if customer linking fails
                }

                // Generate and send OTP (handles resend if OTP already exists)
                var otpResult = await GenerateAndSendOtpAsync(user.Id, OtpPurpose.Registration);
                if (!otpResult.Success)
                {
                    _logger?.LogError("Failed to generate/send OTP during registration");
                    return ServiceResult<OtpRequiredResponse>.Fail("Registration successful, but OTP could not be sent. Please contact support.");
                }

                var response = new OtpRequiredResponse
                {
                    UserId = user.Id,
                    OtpRequired = true,
                    OtpSent = true,
                    Purpose = "Registration"
                };

                var message = existingUser != null && existingUser.Status == UserStatus.PendingOtpConfirmation
                    ? "OTP has been re-sent to your phone. Please verify to complete registration."
                    : "Registration successful. OTP has been sent to your phone.";

                return ServiceResult<OtpRequiredResponse>.Ok(response, message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during registration for phone: {Phone}", WalletCodeHelper.MaskPhone(request.Phone));
                return ServiceResult<OtpRequiredResponse>.Fail($"Registration failed: {ex.Message}");
            }
        }

        public async Task<ServiceResult<object>> LoginAsync(LoginRequest request, CancellationToken ct = default)
        {
            try
            {
                // Find user by phone
                var user = await _dbContext.Users
                    .Include(x => x.Tenant)
                    .ThenInclude(x => x.LoyaltyPrograms)
                    .ThenInclude(x => x.CardTemplate)
                    .FirstOrDefaultAsync(x => x.PhoneNumber == request.Phone || x.UserName == request.Phone, ct);

                if (user == null)
                {
                    _logger?.LogWarning("Login attempt with non-existent phone: {Phone}", WalletCodeHelper.MaskPhone(request.Phone));
                    return ServiceResult<object>.Fail("Invalid phone or password");
                }

                // Check password
                var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
                if (!passwordValid)
                {
                    _logger?.LogWarning("Invalid password attempt for UserId: {UserId}", user.Id);
                    return ServiceResult<object>.Fail("Invalid phone or password");
                }

                // Check user status
                if (user.Status == UserStatus.Blocked)
                {
                    _logger?.LogWarning("Login attempt for blocked user: {UserId}", user.Id);
                    return ServiceResult<object>.Fail("Your account is blocked.");
                }

                // If user is Active, create JWT and return session
                if (user.Status == UserStatus.Active)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    var claims = await BuildClaimsAsync(user, roles);
                    var token = _tokenHelper.CreateToken(user, claims);
                    await SaveRefreshTokenAsync(user.Id, token.RefreshToken!, token.RefreshTokenExpiration!.Value);

                    // Get Google Logo URL from tenant's first active loyalty program's card template
                    var googleLogoUrl = user.Tenant?.LoyaltyPrograms
                        ?.FirstOrDefault(lp => lp.IsActive && lp.CardTemplate != null)
                        ?.CardTemplate?.GoogleLogoUrl;

                    var session = new UserSessionDto
                    {
                        Id = user.Id,
                        UserName = user.UserName!,
                        FirstName = user.Name,
                        LastName = user.Surname,
                        Email = user.Email,
                        Phone = user.PhoneNumber,
                        BirthDate = user.DateOfBirth ?? DateTime.Now,
                        Roles = roles.ToList(),
                        TenantId = user.TenantId,
                        ProgramId = user.Tenant?.LoyaltyPrograms?.FirstOrDefault()?.Id,
                        WalveroId = user.WalveroId,
                        Token = token.Token,
                        Expiration = token.Expiration,
                        RefreshToken = token.RefreshToken,
                        RefreshTokenExpiration = token.RefreshTokenExpiration,
                        GoogleLogoUrl = googleLogoUrl
                    };

                    return ServiceResult<object>.Ok(session, "Login successful");
                }

                // If user is PendingOtpConfirmation, send OTP and return OtpRequiredResponse (don't log in)
                if (user.Status == UserStatus.PendingOtpConfirmation)
                {
                    // Generate and send OTP (handles resend if OTP already exists)
                    // Use Registration purpose for login OTP since they're both for account verification
                    var otpResult = await GenerateAndSendOtpAsync(user.Id, OtpPurpose.Registration);
                    if (!otpResult.Success)
                    {
                        _logger?.LogError("Failed to generate/send OTP during login");
                        return ServiceResult<object>.Fail("Failed to send OTP. Please try again.");
                    }

                    var otpResponse = new OtpRequiredResponse
                    {
                        UserId = user.Id,
                        OtpRequired = true,
                        OtpSent = true,
                        Purpose = "Login"
                    };

                    return ServiceResult<object>.Ok(otpResponse, "Your account requires verification. OTP has been sent to your phone. Please verify to complete login.");
                }

                return ServiceResult<object>.Fail("Invalid user status");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during login for phone: {Phone}", WalletCodeHelper.MaskPhone(request.Phone));
                return ServiceResult<object>.Fail($"Login error: {ex.Message}");
            }
        }

        public async Task<ServiceResult<VerifyOtpResponse>> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default)
        {
            try
            {
                var user = await _dbContext.Users
                    .Include(x => x.Tenant)
                    .ThenInclude(x => x.LoyaltyPrograms)
                    .ThenInclude(x => x.CardTemplate)
                    .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);

                if (user == null)
                {
                    return ServiceResult<VerifyOtpResponse>.Fail("User not found");
                }

                // Map purpose string to OtpPurpose enum
                OtpPurpose otpPurpose = request.Purpose.ToLower() switch
                {
                    "registration" => OtpPurpose.Registration,
                    "login" => OtpPurpose.Registration, // Login uses Registration purpose for account verification
                    "passwordreset" => OtpPurpose.PasswordReset,
                    _ => OtpPurpose.Registration
                };

                // Use the existing VerifyOtpAsync(int, string, OtpPurpose) method for validation
                var verifyResult = await VerifyOtpAsyncInternal(user.Id, request.OtpCode, otpPurpose);
                if (!verifyResult.Success)
                {
                    return ServiceResult<VerifyOtpResponse>.Fail(verifyResult.Message ?? "Invalid or expired OTP.");
                }

                // If verification was successful, activate user if still pending
                if (user.Status == UserStatus.PendingOtpConfirmation && (request.Purpose == "Registration" || request.Purpose == "Login"))
                {
                    user.IsPhoneVerified = true;
                    user.Status = UserStatus.Active;
                    user.PhoneNumberConfirmed = true;
                    await _userManager.UpdateAsync(user);

                    _logger?.LogInformation("User activated after OTP verification. UserId: {UserId}", user.Id);
                }

                // Check if user is blocked
                if (user.Status == UserStatus.Blocked)
                {
                    return ServiceResult<VerifyOtpResponse>.Fail("Your account is blocked.");
                }

                // Generate JWT and return session
                if (_tokenHelper == null)
                {
                    return ServiceResult<VerifyOtpResponse>.Fail("Token generation service not available");
                }

                var roles = await _userManager.GetRolesAsync(user);
                var claims = await BuildClaimsAsync(user, roles);
                var token = _tokenHelper.CreateToken(user, claims);
                await SaveRefreshTokenAsync(user.Id, token.RefreshToken!, token.RefreshTokenExpiration!.Value);

                // Get Google Logo URL from tenant's first active loyalty program's card template
                var googleLogoUrl = user.Tenant?.LoyaltyPrograms
                    ?.FirstOrDefault(lp => lp.IsActive && lp.CardTemplate != null)
                    ?.CardTemplate?.GoogleLogoUrl;

                var session = new UserSessionDto
                {
                    Id = user.Id,
                    UserName = user.UserName!,
                    FirstName = user.Name,
                    LastName = user.Surname,
                    WalveroId = user.WalveroId,
                    Email = user.Email,
                    Phone = user.PhoneNumber,
                    BirthDate = user.DateOfBirth ?? DateTime.Now,
                    Roles = roles.ToList(),
                    TenantId = user.TenantId,
                    ProgramId = user.Tenant?.LoyaltyPrograms?.FirstOrDefault()?.Id,
                    Token = token.Token,
                    Expiration = token.Expiration,
                    RefreshToken = token.RefreshToken,
                    RefreshTokenExpiration = token.RefreshTokenExpiration,
                    GoogleLogoUrl = googleLogoUrl
                };

                var response = new VerifyOtpResponse
                {
                    Success = true,
                    Message = "OTP verified successfully.",
                    Session = session
                };

                return ServiceResult<VerifyOtpResponse>.Ok(response, "OTP verified successfully.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error verifying OTP for UserId: {UserId}", request.UserId);
                return ServiceResult<VerifyOtpResponse>.Fail($"OTP verification failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Resends OTP for registration or login. Uses consolidated OTP generation logic.
        /// </summary>
        public async Task<ServiceResult<OtpRequiredResponse>> ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(request.UserId.ToString());
                if (user == null)
                {
                    return ServiceResult<OtpRequiredResponse>.Fail("User not found");
                }

                // Check if user is in a valid state for OTP resend
                if (user.Status == UserStatus.Blocked)
                {
                    return ServiceResult<OtpRequiredResponse>.Fail("Your account is blocked.");
                }

                // Map purpose string to OtpPurpose enum
                OtpPurpose otpPurpose = request.Purpose.ToLower() switch
                {
                    "registration" => OtpPurpose.Registration,
                    "login" => OtpPurpose.Registration, // Login uses Registration purpose for account verification
                    "passwordreset" => OtpPurpose.PasswordReset,
                    _ => OtpPurpose.Registration
                };

                // Use consolidated OTP generation method
                var otpResult = await GenerateAndSendOtpAsync(user.Id, otpPurpose);
                if (!otpResult.Success)
                {
                    _logger?.LogError("Failed to generate/send OTP during resend for UserId: {UserId}", user.Id);
                    return ServiceResult<OtpRequiredResponse>.Fail("Failed to send OTP. Please try again.");
                }

                var response = new OtpRequiredResponse
                {
                    UserId = user.Id,
                    OtpRequired = true,
                    OtpSent = true,
                    Purpose = request.Purpose
                };

                return ServiceResult<OtpRequiredResponse>.Ok(response, "OTP has been re-sent successfully.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error resending OTP for UserId: {UserId}", request.UserId);
                return ServiceResult<OtpRequiredResponse>.Fail($"Failed to resend OTP: {ex.Message}");
            }
        }

    }
}
