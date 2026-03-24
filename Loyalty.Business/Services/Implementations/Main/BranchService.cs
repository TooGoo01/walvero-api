using Loyalty.Business.Dtos.Get.Branch;
using Loyalty.Business.Dtos.Post.Branch;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class BranchService : IBranchService
    {
        private readonly IBranchRepository _branchRepository;
        private readonly IUnitOfWork _unitOfWork;

        public BranchService(IBranchRepository branchRepository, IUnitOfWork unitOfWork)
        {
            _branchRepository = branchRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<ServiceResult> ListAsync(int tenantId, CancellationToken ct = default)
        {
            var branches = await _branchRepository.GetByTenantAsync(tenantId, ct);
            var dtos = branches.Select(b => MapToDto(b)).ToList();
            return new ServiceResult(true, dtos);
        }

        public async Task<ServiceResult> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var branch = await _branchRepository.GetByIdAsync(id, ct);
            if (branch == null)
                return new ServiceResult(false, "Filial tapılmadı.");

            return new ServiceResult(true, MapToDto(branch));
        }

        public async Task<ServiceResult> CreateAsync(int tenantId, CreateBranchDto dto, CancellationToken ct = default)
        {
            var branch = new Branch
            {
                TenantId = tenantId,
                Name = dto.Name,
                Address = dto.Address,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                RadiusMeters = dto.RadiusMeters,
                Phone = dto.Phone,
                Email = dto.Email,
                PhotoUrl = dto.PhotoUrl,
                WorkingHoursJson = dto.WorkingHoursJson,
                IsMainBranch = dto.IsMainBranch,
                IsActive = true
            };

            await _unitOfWork.Repository<Branch>().AddAsync(branch);
            _unitOfWork.Commit();

            return new ServiceResult(true, MapToDto(branch));
        }

        public async Task<ServiceResult> UpdateAsync(int id, UpdateBranchDto dto, CancellationToken ct = default)
        {
            var branch = await _branchRepository.GetByIdAsync(id, ct);
            if (branch == null)
                return new ServiceResult(false, "Filial tapılmadı.");

            branch.Name = dto.Name;
            branch.Address = dto.Address;
            branch.Latitude = dto.Latitude;
            branch.Longitude = dto.Longitude;
            branch.RadiusMeters = dto.RadiusMeters;
            branch.Phone = dto.Phone;
            branch.Email = dto.Email;
            branch.PhotoUrl = dto.PhotoUrl;
            branch.WorkingHoursJson = dto.WorkingHoursJson;
            branch.IsMainBranch = dto.IsMainBranch;
            branch.EditDate = DateTime.UtcNow;

            _unitOfWork.Repository<Branch>().Update(branch);
            _unitOfWork.Commit();

            return new ServiceResult(true, MapToDto(branch));
        }

        public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
        {
            var branch = await _branchRepository.GetByIdAsync(id, ct);
            if (branch == null)
                return new ServiceResult(false, "Filial tapılmadı.");

            branch.IsActive = false;
            branch.EditDate = DateTime.UtcNow;

            _unitOfWork.Repository<Branch>().Update(branch);
            _unitOfWork.Commit();

            return new ServiceResult(true, "Filial deaktiv edildi.");
        }

        public async Task<ServiceResult> GetNearbyAsync(int tenantId, double lat, double lng, double radiusKm, CancellationToken ct = default)
        {
            var branches = await _branchRepository.GetNearbyAsync(tenantId, lat, lng, radiusKm, ct);
            var dtos = branches.Select(b =>
            {
                var dto = MapToDto(b);
                // Calculate distance for the DTO
                if (b.Latitude.HasValue && b.Longitude.HasValue)
                {
                    dto.DistanceKm = CalculateHaversineDistance(lat, lng, b.Latitude.Value, b.Longitude.Value);
                }
                return dto;
            }).ToList();

            return new ServiceResult(true, dtos);
        }

        public async Task<ServiceResult> GetStatsAsync(int branchId, CancellationToken ct = default)
        {
            var branch = await _branchRepository.GetByIdAsync(branchId, ct);
            if (branch == null)
                return new ServiceResult(false, "Filial tapılmadı.");

            var transactions = branch.Transactions ?? new List<Transaction>();

            var stats = new
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                TotalTransactions = transactions.Count,
                TotalPointsEarned = transactions
                    .Where(t => t.Type == TransactionType.Earned)
                    .Sum(t => t.Points),
                TotalPointsRedeemed = transactions
                    .Where(t => t.Type == TransactionType.Redeemed)
                    .Sum(t => t.Points),
                StaffCount = branch.Staff?.Count ?? 0
            };

            return new ServiceResult(true, stats);
        }

        private static GetBranchDto MapToDto(Branch b) => new()
        {
            Id = b.Id,
            TenantId = b.TenantId,
            Name = b.Name,
            Address = b.Address,
            Latitude = b.Latitude,
            Longitude = b.Longitude,
            RadiusMeters = b.RadiusMeters,
            Phone = b.Phone,
            Email = b.Email,
            PhotoUrl = b.PhotoUrl,
            WorkingHoursJson = b.WorkingHoursJson,
            IsMainBranch = b.IsMainBranch,
            IsActive = b.IsActive,
            RegDate = b.RegDate,
            StaffCount = b.Staff?.Count ?? 0
        };

        private static double CalculateHaversineDistance(double lat1, double lng1, double lat2, double lng2)
        {
            var lat1Rad = lat1 * Math.PI / 180.0;
            var lat2Rad = lat2 * Math.PI / 180.0;
            var lng1Rad = lng1 * Math.PI / 180.0;
            var lng2Rad = lng2 * Math.PI / 180.0;

            var cosDistance = Math.Cos(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(lng2Rad - lng1Rad)
                           + Math.Sin(lat1Rad) * Math.Sin(lat2Rad);

            cosDistance = Math.Max(-1.0, Math.Min(1.0, cosDistance));
            return Math.Round(6371.0 * Math.Acos(cosDistance), 2);
        }
    }
}
