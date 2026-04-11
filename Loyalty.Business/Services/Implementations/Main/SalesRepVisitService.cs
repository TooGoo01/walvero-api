using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class SalesRepVisitService : ISalesRepVisitService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ISalesRepVisitRepository _repo;

        public SalesRepVisitService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ISalesRepVisitRepository repo)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _repo = repo;
        }

        public async Task<ServiceResult> AddAsync(AddSalesRepVisitDto dto, CancellationToken ct = default)
        {
            try
            {
                var entity = _mapper.Map<SalesRepVisit>(dto);
                entity.Status = (SalesVisitStatus)dto.Status;
                entity.VisitDate = dto.VisitDate ?? DateTime.UtcNow;
                entity.RegDate = DateTime.UtcNow;
                entity.IsActive = true;

                await _unitOfWork.Repository<SalesRepVisit>().AddAsync(entity);
                _unitOfWork.Commit();

                var response = ToGetDto(entity);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> UpdateAsync(int id, UpdateSalesRepVisitDto dto, CancellationToken ct = default)
        {
            try
            {
                var entity = await _repo.GetByIdAsync(id, ct);
                if (entity == null)
                    return new ServiceResult(false, "Sales rep visit not found");

                entity.SalesRepName = dto.SalesRepName;
                entity.VenueName = dto.VenueName;
                entity.VenueAddress = dto.VenueAddress;
                entity.Latitude = dto.Latitude;
                entity.Longitude = dto.Longitude;
                entity.ContactPhone = dto.ContactPhone;
                entity.ContactName = dto.ContactName;
                entity.Notes = dto.Notes;
                entity.Status = (SalesVisitStatus)dto.Status;
                entity.RemindFlag = dto.RemindFlag;
                entity.RemindAt = dto.RemindAt;
                if (dto.VisitDate.HasValue)
                    entity.VisitDate = dto.VisitDate.Value;
                entity.EditDate = DateTime.UtcNow;

                _unitOfWork.Repository<SalesRepVisit>().Update(entity);
                _unitOfWork.Commit();

                return new ServiceResult(true, ToGetDto(entity));
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
        {
            try
            {
                var entity = await _repo.GetByIdAsync(id, ct);
                if (entity == null)
                    return new ServiceResult(false, "Sales rep visit not found");

                entity.IsActive = false;
                entity.EditDate = DateTime.UtcNow;
                _unitOfWork.Repository<SalesRepVisit>().Update(entity);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var entity = await _repo.GetByIdAsync(id, ct);
            if (entity == null)
                return new ServiceResult(false, "Sales rep visit not found");

            return new ServiceResult(true, ToGetDto(entity));
        }

        public async Task<ServiceResult> ListAsync(SalesRepVisitFilterDto filter, CancellationToken ct = default)
        {
            var list = await _repo.ListAsync(filter.Status, filter.RemindOnly, filter.SearchText, ct);
            var result = list.Select(ToGetDto).ToList();
            return new ServiceResult(true, result);
        }

        private static GetSalesRepVisitDto ToGetDto(SalesRepVisit e) => new()
        {
            Id = e.Id,
            SalesRepName = e.SalesRepName,
            VenueName = e.VenueName,
            VenueAddress = e.VenueAddress,
            Latitude = e.Latitude,
            Longitude = e.Longitude,
            ContactPhone = e.ContactPhone,
            ContactName = e.ContactName,
            Notes = e.Notes,
            Status = (int)e.Status,
            StatusName = e.Status.ToString(),
            RemindFlag = e.RemindFlag,
            RemindAt = e.RemindAt,
            VisitDate = e.VisitDate,
            RegDate = e.RegDate,
            IsActive = e.IsActive
        };
    }
}
