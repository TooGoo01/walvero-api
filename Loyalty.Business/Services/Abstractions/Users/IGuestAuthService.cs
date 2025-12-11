using Loyalty.Business.Dtos.Get.Users;
using Loyalty.Business.Dtos.Post.Users;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Users
{
    public interface IGuestAuthService
    {
        Task<ServiceResult> GuestLoginAsync(GuestLoginDto dto);
        Task<ServiceResult> CompleteProfileAsync(CompleteProfileDto dto);
        Task<ServiceResult> GetProfileStatusAsync();
        Task<ServiceResult> RefreshTokenAsync(RefreshTokenDto dto);
    }
}

