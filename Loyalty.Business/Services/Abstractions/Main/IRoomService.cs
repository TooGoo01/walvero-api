using Loyalty.Business.Dtos.Post.Main.Room;
using Loyalty.Business.Results;
using Microsoft.AspNetCore.Http;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IRoomService
    {
        Task<ServiceResult> ListAsync(int branchId);
        Task<ServiceResult> GetByIdAsync(int id);
        Task<ServiceResult> CreateAsync(CreateRoomDto dto);
        Task<ServiceResult> UpdateAsync(int id, UpdateRoomDto dto);
        Task<ServiceResult> DeleteAsync(int id);

        Task<ServiceResult> UploadMediaAsync(int roomId, IFormFileCollection files);
        Task<ServiceResult> DeleteMediaAsync(int fileId);
        Task<ServiceResult> ReorderMediaAsync(int roomId, ReorderRoomMediaDto dto);
    }
}
