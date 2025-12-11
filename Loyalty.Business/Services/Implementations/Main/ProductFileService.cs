using AutoMapper;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.UnitOfWorks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class ProductFileService : IProducFileService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStorage _storage;
        private readonly IMapper _mapper;
        public ProductFileService(IUnitOfWork unitOfWork, IStorage storage, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _mapper = mapper;
        }

        public async Task<ServiceResult> AddRangeAsync(AddProductDto aboutDto, int aboutId)
        {
            List<(string fileName, string pathOrContainerName)> result = await _storage.UploadAsync("photo-about", aboutDto.Files);
            var about = _unitOfWork.Repository<Product>().Get(x => x.Id == aboutId);
            _unitOfWork.Repository<ProductFile>().AddRange(result.Select(x => new ProductFile
            {
                FileName = x.fileName,
                Path = x.pathOrContainerName,
                Product = about
            }));
            _unitOfWork.Commit();
            return new ServiceResult(true);
        }

        public async Task<ServiceResult> UpdateRangeAsync(AddProductDto aboutDto, int aboutId)
        {
            List<(string fileName, string pathOrContainerName)> result = await _storage.UploadAsync("photo-about", aboutDto.Files);
            var about = _unitOfWork.Repository<Product>().Get(x => x.Id == aboutId);
            var entity = await _unitOfWork.Repository<ProductFile>().GetAllAsync(x => x.Product.Id == aboutId);
            _unitOfWork.Repository<ProductFile>().DeleteRange(entity);
            _unitOfWork.Repository<ProductFile>().UpdateRange(result.Select(x => new ProductFile
            {
                FileName = x.fileName,
                Path = x.pathOrContainerName,
                Product = about
            }));
            _unitOfWork.Commit();
            return new ServiceResult(true);
        }
    }
}
