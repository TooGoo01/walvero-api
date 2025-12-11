using AutoMapper;
using Loyalty.Business.Dtos.Get.Users;
using Loyalty.Business.Dtos.Post.Users;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Users;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Models;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Utilities.Security.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Data;
using System.Security.Claims;

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
            IHttpContextAccessor httpContextAccessor = null)
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

                            var detail = new UserSessionDto
                            {
                                Id = user.Id,
                                UserName = user.UserName,
                                FirstName = user.Name,
                                LastName = user.Surname,
                                Email = user.Email,
                                BirthDate = user.DateOfBirth != null ? user.DateOfBirth : DateTime.Now,
                                Roles = rolem,
                                TenantId = user.TenantId,
                                Token = token.Token,
                                Expiration = token.Expiration,
                                RefreshToken = token.RefreshToken,
                                RefreshTokenExpiration = token.RefreshTokenExpiration
                            };
                            
                            return new ServiceResult(true, detail, "login success");
                        }
                    }
                }

                // Normal login uğursuz oldu və ya UserName/Password verilməyib
                // Guest login istifadə et
                if (_guestAuthService == null)
                {
                    return new ServiceResult(false, "Guest auth service not available");
                }

                var guestLoginDto = new GuestLoginDto
                {
                    DeviceId = loginUser.DeviceId
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
                        RefreshTokenExpiration = guestResponse.RefreshTokenExpiration
                    };
                    
                    return new ServiceResult(true, detail, "guest login success");
                }

                return new ServiceResult(false, guestResult.Message ?? "Login failed");
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
                // Register artıq guest login kimi işləyir
                // DeviceId tələb olunur
                if (string.IsNullOrEmpty(registerUser.DeviceId))
                {
                    return new ServiceResult(false, "DeviceId is required for registration");
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
                        RefreshTokenExpiration = guestResponse.RefreshTokenExpiration
                    };
                    
                    return new ServiceResult(true, detail, "guest registration success");
                }

                return new ServiceResult(false, guestResult.Message ?? "Registration failed");
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
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
                var result = await _dbContext.Users.Include(x=>x.Tenant).ThenInclude(x=>x.LoyaltyPrograms).FirstOrDefaultAsync(x=>x.UserName==userName);

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
    }
}
