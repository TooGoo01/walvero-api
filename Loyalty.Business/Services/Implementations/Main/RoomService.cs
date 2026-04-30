using AutoMapper;
using Loyalty.Business.Dtos.Get.Main.Room;
using Loyalty.Business.Dtos.Post.Main.Room;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RoomEntity = Loyalty.DataAccess.Entities.Main.Room;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class RoomService : IRoomService
    {
        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".webm", ".m4v", ".avi", ".mkv"
        };

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly IStorage _storage;

        public RoomService(IUnitOfWork unitOfWork, IMapper mapper, LoyaltyWalletDbContext dbContext, IStorage storage)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _dbContext = dbContext;
            _storage = storage;
        }

        public async Task<ServiceResult> ListAsync(int branchId)
        {
            var rooms = await _dbContext.Rooms
                .AsNoTracking()
                .Where(r => r.BranchId == branchId && r.IsActive)
                .Include(r => r.Files.Where(f => f.IsActive))
                .OrderBy(r => r.OrderIndex)
                .ThenBy(r => r.Id)
                .ToListAsync();

            var dtos = _mapper.Map<List<GetRoomDto>>(rooms);
            return new ServiceResult(true, dtos);
        }

        public async Task<ServiceResult> GetByIdAsync(int id)
        {
            var room = await _dbContext.Rooms
                .AsNoTracking()
                .Include(r => r.Files.Where(f => f.IsActive).OrderBy(f => f.OrderIndex))
                .FirstOrDefaultAsync(r => r.Id == id);

            if (room == null)
                return new ServiceResult(false, "Room not found");

            var dto = _mapper.Map<GetRoomDto>(room);
            return new ServiceResult(true, dto);
        }

        public async Task<ServiceResult> CreateAsync(CreateRoomDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return new ServiceResult(false, "Room name is required");

            var branchExists = await _dbContext.Branches.AnyAsync(b => b.Id == dto.BranchId && b.IsActive);
            if (!branchExists)
                return new ServiceResult(false, "Branch not found");

            var entity = _mapper.Map<RoomEntity>(dto);
            entity.IsActive = true;

            await _unitOfWork.Repository<RoomEntity>().AddAsync(entity);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetRoomDto>(entity);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> UpdateAsync(int id, UpdateRoomDto dto)
        {
            var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.Id == id);
            if (room == null)
                return new ServiceResult(false, "Room not found");

            if (dto.Name != null)
            {
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return new ServiceResult(false, "Room name cannot be empty");
                room.Name = dto.Name;
            }
            if (dto.Type != null) room.Type = dto.Type;
            if (dto.Description != null) room.Description = dto.Description;
            if (dto.Capacity.HasValue) room.Capacity = dto.Capacity;
            if (dto.OrderIndex.HasValue) room.OrderIndex = dto.OrderIndex.Value;

            if (dto.ClearMinDeposit == true)
            {
                room.MinDeposit = null;
            }
            else if (dto.MinDeposit.HasValue)
            {
                room.MinDeposit = dto.MinDeposit;
            }

            _unitOfWork.Repository<RoomEntity>().Update(room);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetRoomDto>(room);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.Id == id);
            if (room == null)
                return new ServiceResult(false, "Room not found");

            room.IsActive = false;
            _unitOfWork.Repository<RoomEntity>().Update(room);
            _unitOfWork.Commit();
            return new ServiceResult(true);
        }

        public async Task<ServiceResult> UploadMediaAsync(int roomId, IFormFileCollection files)
        {
            if (files == null || files.Count == 0)
                return new ServiceResult(false, "No files provided");

            var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.Id == roomId && r.IsActive);
            if (room == null)
                return new ServiceResult(false, "Room not found");

            var maxOrder = await _dbContext.RoomFiles
                .Where(f => f.RoomId == roomId && f.IsActive)
                .Select(f => (int?)f.OrderIndex)
                .MaxAsync() ?? -1;

            var uploaded = await _storage.UploadAsync("room-media", files);

            var newFiles = new List<RoomFile>();
            int idx = 0;
            foreach (var file in files)
            {
                var stored = uploaded[idx];
                var ext = Path.GetExtension(stored.fileName);
                var mediaType = !string.IsNullOrEmpty(file.ContentType) && file.ContentType.StartsWith("video", StringComparison.OrdinalIgnoreCase)
                    ? "video"
                    : (VideoExtensions.Contains(ext ?? string.Empty) ? "video" : "image");

                newFiles.Add(new RoomFile
                {
                    RoomId = roomId,
                    FileName = stored.fileName,
                    Path = stored.pathOrContainerName,
                    MediaType = mediaType,
                    OrderIndex = ++maxOrder,
                    IsActive = true
                });
                idx++;
            }

            _unitOfWork.Repository<RoomFile>().AddRange(newFiles);
            _unitOfWork.Commit();

            var dtos = _mapper.Map<List<GetRoomFileDto>>(newFiles);
            return new ServiceResult(true, dtos);
        }

        public async Task<ServiceResult> DeleteMediaAsync(int fileId)
        {
            var file = await _dbContext.RoomFiles.FirstOrDefaultAsync(f => f.Id == fileId);
            if (file == null)
                return new ServiceResult(false, "File not found");

            file.IsActive = false;
            _unitOfWork.Repository<RoomFile>().Update(file);
            _unitOfWork.Commit();

            try
            {
                await _storage.DeleteAsync("room-media", file.FileName);
            }
            catch
            {
                // Storage delete is best-effort; row is already soft-deleted.
            }

            return new ServiceResult(true);
        }

        public async Task<ServiceResult> ReorderMediaAsync(int roomId, ReorderRoomMediaDto dto)
        {
            if (dto?.Items == null || dto.Items.Count == 0)
                return new ServiceResult(false, "No items provided");

            var ids = dto.Items.Select(i => i.FileId).ToList();
            var files = await _dbContext.RoomFiles
                .Where(f => f.RoomId == roomId && ids.Contains(f.Id))
                .ToListAsync();

            var orderMap = dto.Items.ToDictionary(i => i.FileId, i => i.OrderIndex);
            foreach (var f in files)
            {
                if (orderMap.TryGetValue(f.Id, out var newOrder))
                {
                    f.OrderIndex = newOrder;
                    _unitOfWork.Repository<RoomFile>().Update(f);
                }
            }
            _unitOfWork.Commit();
            return new ServiceResult(true);
        }
    }
}
