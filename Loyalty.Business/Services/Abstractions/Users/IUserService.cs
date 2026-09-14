using Loyalty.Business.Dtos.Get.Users;
using Loyalty.Business.Dtos.Post.Users;
using Loyalty.Business.Results;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Services.Abstractions.Users
{
    public interface IUserService
    {
        Task<ServiceResult> GetUser(string userId);
        Task<ServiceResult> GetUserByName(string userName);
        Task<ServiceResult> DeleteUser(string userId);
        Task<ServiceResult> AddUser(RegisterUserDto registerUser);
        Task<ServiceResult> Register(RegisterUserDto registerUser);
        Task<ServiceResult> LogIn(LoginUserDto loginUser);
        Task<ServiceResult> LogOut();
        Task<ServiceResult> AddClaim(AddClaimDto addClaim);
        Task<ServiceResult> AddRole(AddRoleModel model);
        Task<ServiceResult> AddUserRole(int userId, AddRoleModel model);
        Task<ServiceResult> GetRole();
        Task<ServiceResult> GetUsers();
        Task<ServiceResult> GetLoggedUser();
        Task<ServiceResult> PasswordResetAsync(string email);
        Task<bool> VerifyResetTokenAsync(string resetToke, string UserName);
        Task<bool> UpdatePassword(string userName, string resetToken, string newPassword);
        Task<bool> EmailConfirmAsync(string email);
        Task<bool> VerifyConfirmAsync(string userName, string confirmToken);
        Task<ServiceResult> Verify(string phone, string randomString);
        
        // Phone-based OTP registration methods (legacy)
        Task<ServiceResult> RegisterUserWithPendingStatusAsync(RegisterWithPhoneDto registerDto, CancellationToken ct = default);
        Task<ServiceResult> VerifyOtpAsync(int userId, string otpCode, OtpPurpose purpose);
        
        // Phone-based password reset methods
        Task<ServiceResult> StartForgotPasswordAsync(string phoneNumber, string? channel = null);
        Task<ServiceResult> VerifyForgotPasswordOtpAsync(string phoneNumber, string otpCode);
        Task<ServiceResult> ResetPasswordAsync(string resetToken, string newPassword);

        // Main OTP authentication flow methods
        Task<ServiceResult<OtpRequiredResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
        Task<ServiceResult<object>> LoginAsync(LoginRequest request, CancellationToken ct = default);
        Task<ServiceResult<VerifyOtpResponse>> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken ct = default);
        Task<ServiceResult<OtpRequiredResponse>> ResendOtpAsync(ResendOtpRequest request, CancellationToken ct = default);

        // Profile update methods
        Task<ServiceResult<UpdateProfileResponseDto>> UpdateProfileAsync(UpdateProfileDto dto, CancellationToken ct = default);
        Task<ServiceResult<ApplicationUserDto>> GetMyProfileAsync(CancellationToken ct = default);
        Task<ServiceResult> VerifyOldPhoneForUpdateAsync(string otpCode, CancellationToken ct = default);
        Task<ServiceResult> VerifyNewPhoneForUpdateAsync(string newPhoneNumber, string otpCode, CancellationToken ct = default);
        Task<ServiceResult> ResendPhoneUpdateOtpAsync(ResendPhoneUpdateOtpDto dto, CancellationToken ct = default);

        // Admin user management
        Task<ServiceResult> ListUsersAsync();
        Task<ServiceResult> GetRolesAsync();
        Task<ServiceResult> CreateAdminUserAsync(CreateAdminUserDto dto);
        Task<ServiceResult> UpdateAdminUserAsync(string userId, string? role, int? tenantId, List<string>? roles = null, int? branchId = null, bool clearBranchId = false);

        // Program switching
        Task<ServiceResult<object>> SwitchProgramAsync(int programId, CancellationToken ct = default);
    }
}
