using AutoMapper;
using Loyalty.Business.Dtos.Get.Main.Branch;
using Loyalty.Business.Dtos.Post.Main.Branch;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using BranchEntity = Loyalty.DataAccess.Entities.Main.Branch;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class BranchService : IBranchService
    {
        private readonly IBranchRepository _branchRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly IStorage _storage;

        public BranchService(
            IBranchRepository branchRepository,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            LoyaltyWalletDbContext dbContext,
            IStorage storage)
        {
            _branchRepository = branchRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _dbContext = dbContext;
            _storage = storage;
        }

        public async Task<ServiceResult> ListAsync(int? tenantId)
        {
            var query = _dbContext.Branches.AsNoTracking().Where(b => b.IsActive);
            if (tenantId.HasValue)
                query = query.Where(b => b.TenantId == tenantId.Value);

            var branches = await query
                .OrderByDescending(b => b.IsMainBranch)
                .ThenBy(b => b.Name)
                .ToListAsync();

            var dtos = _mapper.Map<List<GetBranchDto>>(branches);
            foreach (var d in dtos)
            {
                d.StaffCount = 0;
            }

            return new ServiceResult(true, dtos);
        }

        public async Task<ServiceResult> GetByIdAsync(int id)
        {
            var branch = await _dbContext.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
            if (branch == null)
                return new ServiceResult(false, "Branch not found");

            var dto = _mapper.Map<GetBranchDto>(branch);
            dto.StaffCount = 0;
            return new ServiceResult(true, dto);
        }

        public async Task<ServiceResult> CreateAsync(CreateBranchDto dto, int tenantId)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return new ServiceResult(false, "Branch name is required");

            var entity = _mapper.Map<BranchEntity>(dto);
            entity.TenantId = tenantId;
            entity.IsActive = true;

            if (entity.IsMainBranch)
            {
                await ResetMainBranchAsync(tenantId);
            }
            else
            {
                var hasMain = await _dbContext.Branches.AnyAsync(b => b.TenantId == tenantId && b.IsActive && b.IsMainBranch);
                if (!hasMain)
                    entity.IsMainBranch = true;
            }

            await _unitOfWork.Repository<BranchEntity>().AddAsync(entity);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetBranchDto>(entity);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> UpdateAsync(int id, UpdateBranchDto dto)
        {
            var branch = await _dbContext.Branches.FirstOrDefaultAsync(b => b.Id == id);
            if (branch == null)
                return new ServiceResult(false, "Branch not found");

            if (dto.Name != null) branch.Name = dto.Name;
            if (dto.Address != null) branch.Address = dto.Address;
            if (dto.Latitude.HasValue) branch.Latitude = dto.Latitude;
            if (dto.Longitude.HasValue) branch.Longitude = dto.Longitude;
            if (dto.RadiusMeters.HasValue) branch.RadiusMeters = dto.RadiusMeters.Value;
            if (dto.Phone != null) branch.Phone = dto.Phone;
            if (dto.Email != null) branch.Email = dto.Email;
            if (dto.PhotoUrl != null) branch.PhotoUrl = dto.PhotoUrl;
            if (dto.LogoUrl != null) branch.LogoUrl = dto.LogoUrl;
            if (dto.WorkingHoursJson != null) branch.WorkingHoursJson = dto.WorkingHoursJson;
            if (dto.LocationUrl != null) branch.LocationUrl = dto.LocationUrl;
            if (dto.WazeLocationUrl != null) branch.WazeLocationUrl = dto.WazeLocationUrl;
            if (dto.MenuUrl != null) branch.MenuUrl = dto.MenuUrl;
            if (dto.InstagramUrl != null) branch.InstagramUrl = dto.InstagramUrl;
            if (dto.FacebookUrl != null) branch.FacebookUrl = dto.FacebookUrl;
            if (dto.WhatsAppUrl != null) branch.WhatsAppUrl = dto.WhatsAppUrl;
            if (dto.TelegramUrl != null) branch.TelegramUrl = dto.TelegramUrl;
            if (dto.LinkedInUrl != null) branch.LinkedInUrl = dto.LinkedInUrl;
            if (dto.TiktokUrl != null) branch.TiktokUrl = dto.TiktokUrl;
            if (dto.BackgroundColor != null) branch.BackgroundColor = dto.BackgroundColor;
            if (dto.ForegroundColor != null) branch.ForegroundColor = dto.ForegroundColor;

            if (dto.IsMainBranch.HasValue && dto.IsMainBranch.Value && !branch.IsMainBranch)
            {
                await ResetMainBranchAsync(branch.TenantId);
                branch.IsMainBranch = true;
            }
            else if (dto.IsMainBranch.HasValue && !dto.IsMainBranch.Value)
            {
                branch.IsMainBranch = false;
            }

            _unitOfWork.Repository<BranchEntity>().Update(branch);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetBranchDto>(branch);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            var branch = await _dbContext.Branches.FirstOrDefaultAsync(b => b.Id == id);
            if (branch == null)
                return new ServiceResult(false, "Branch not found");

            branch.IsActive = false;
            _unitOfWork.Repository<BranchEntity>().Update(branch);
            _unitOfWork.Commit();

            return new ServiceResult(true);
        }

        public async Task<ServiceResult> GetStatsAsync(int branchId)
        {
            var branch = await _dbContext.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId);
            if (branch == null)
                return new ServiceResult(false, "Branch not found");

            var stats = new GetBranchStatsDto
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                StaffCount = 0,
                TotalTransactions = 0,
                TotalPointsIssued = 0m,
                TotalPointsRedeemed = 0m
            };

            return new ServiceResult(true, stats);
        }

        public async Task<ServiceResult> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return new ServiceResult(false, "No file provided");

            // Mövcud `_storage.UploadAsync` IFormFileCollection qəbul edir, ona görə tək faylı
            // FormFileCollection-a paketlə.
            var collection = new FormFileCollection { file };
            var uploaded = await _storage.UploadAsync("branch-images", collection);
            if (uploaded.Count == 0)
                return new ServiceResult(false, "Upload failed");

            var (fileName, url) = uploaded[0];
            return new ServiceResult(true, new { url, fileName });
        }

        private async Task ResetMainBranchAsync(int tenantId)
        {
            var existingMains = await _dbContext.Branches
                .Where(b => b.TenantId == tenantId && b.IsMainBranch && b.IsActive)
                .ToListAsync();
            foreach (var m in existingMains)
            {
                m.IsMainBranch = false;
                _unitOfWork.Repository<BranchEntity>().Update(m);
            }
            if (existingMains.Count > 0)
                _unitOfWork.Commit();
        }
    }
}
