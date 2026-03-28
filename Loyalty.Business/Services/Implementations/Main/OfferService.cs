using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.AspNetCore.Http;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class OfferService : IOfferService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IOfferRepository _offerRepository;
        private readonly IStorage _storage;
        private readonly IMediaProcessor _mediaProcessor;

        public OfferService(IOfferRepository offerRepository, IMapper mapper, IUnitOfWork unitOfWork, IStorage storage, IMediaProcessor mediaProcessor)
        {
            _offerRepository = offerRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _storage = storage;
            _mediaProcessor = mediaProcessor;
        }

        public async Task<ServiceResult> AddOffer(AddOfferDto offer)
        {
            var request = _mapper.Map<Offer>(offer);
            request.IsActive = true;

            // Handle file upload
            var container = $"offers-tenant-{offer.TenantId ?? 0}";
            request.PhotoUrl = await UploadOrUseUrl(container, "photo", offer.PhotoFile, offer.PhotoUrl, null);

            await _unitOfWork.Repository<Offer>().AddAsync(request);
            _unitOfWork.Commit();
            var response = _mapper.Map<GetOfferDto>(request);
            return new ServiceResult(true, response.Id);
        }

        public async Task<ServiceResult> DeleteOffer(int id)
        {
            var offer = await _offerRepository.GetOffer(id);
            if (offer != null)
            {
                offer.IsActive = false;
                _unitOfWork.Repository<Offer>().Update(offer);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetActiveOffers()
        {
            var offers = await _offerRepository.GetActiveOffers();
            if (offers != null)
            {
                var response = _mapper.Map<ICollection<GetOfferDto>>(offers);
                return new ServiceResult(true, response);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetOffers()
        {
            var offers = await _offerRepository.GetAllOffers();
            if (offers != null)
            {
                var response = _mapper.Map<ICollection<GetOfferDto>>(offers);
                return new ServiceResult(true, response);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetOffer(int id)
        {
            try
            {
                var result = await _offerRepository.GetOffer(id);
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = _mapper.Map<GetOfferDto>(result);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> GetOffersByTenant(int tenantId)
        {
            var offers = await _offerRepository.GetOffersByTenant(tenantId);
            if (offers != null)
            {
                var response = _mapper.Map<ICollection<GetOfferDto>>(offers);
                return new ServiceResult(true, response);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> UpdateOffer(AddOfferDto offerDto, int id)
        {
            var offer = await _offerRepository.GetOffer(id);
            if (offer != null)
            {
                if (!string.IsNullOrWhiteSpace(offerDto.AzTitle))
                {
                    offer.AzTitle = offerDto.AzTitle.Trim();
                }

                if (!string.IsNullOrWhiteSpace(offerDto.EnTitle))
                {
                    offer.EnTitle = offerDto.EnTitle.Trim();
                }

                if (!string.IsNullOrWhiteSpace(offerDto.RuTitle))
                {
                    offer.RuTitle = offerDto.RuTitle.Trim();
                }

                if (!string.IsNullOrWhiteSpace(offerDto.AzDescription))
                {
                    offer.AzDescription = offerDto.AzDescription.Trim();
                }

                if (!string.IsNullOrWhiteSpace(offerDto.EnDescription))
                {
                    offer.EnDescription = offerDto.EnDescription.Trim();
                }

                if (!string.IsNullOrWhiteSpace(offerDto.RuDescription))
                {
                    offer.RuDescription = offerDto.RuDescription.Trim();
                }

                if (offerDto.TenantId.HasValue)
                {
                    offer.TenantId = offerDto.TenantId;
                }

                // Handle file upload
                var container = $"offers-tenant-{offerDto.TenantId ?? offer.TenantId ?? 0}";
                offer.PhotoUrl = await UploadOrUseUrl(container, "photo", offerDto.PhotoFile, offerDto.PhotoUrl, offer.PhotoUrl);

                _unitOfWork.Repository<Offer>().Update(offer);
                _unitOfWork.Commit();
                var response = _mapper.Map<GetOfferDto>(offer);
                return new ServiceResult(true, response.Id);
            }
            return new ServiceResult(false);
        }

        private async Task<string?> UploadOrUseUrl(string container, string prefix, IFormFile? file, string? url, string? oldValue)
        {
            if (file is not null && file.Length > 0)
            {
                // Convert image to WebP
                if (_mediaProcessor.IsImage(file))
                    file = await _mediaProcessor.ConvertImageToWebpAsync(file);

                // Delete old file if exists
                if (!string.IsNullOrWhiteSpace(oldValue))
                {
                    var oldName = GetFileNameSafe(oldValue);
                    if (!string.IsNullOrEmpty(oldName))
                        await _storage.DeleteAsync(container, oldName);
                }

                var uploaded = await _storage.UploadFileAsync(container, new[] { file });
                var (_, relPath) = uploaded.First();
                return EnsureRelative(relPath);
            }
            else if (!string.IsNullOrWhiteSpace(url))
            {
                return url.Trim();
            }
            return oldValue;
        }

        private static string EnsureRelative(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            var p = path.Replace("\\", "/");
            var idx = p.IndexOf("/resource/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return p[idx..];
            idx = p.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? p[idx..] : p.TrimStart('/');
        }

        private static string? GetFileNameSafe(string? urlOrPath)
        {
            if (string.IsNullOrWhiteSpace(urlOrPath)) return null;
            var u = urlOrPath.Replace("\\", "/");
            var last = u.Split('/').LastOrDefault();
            return string.IsNullOrWhiteSpace(last) ? null : last;
        }
    }
}
