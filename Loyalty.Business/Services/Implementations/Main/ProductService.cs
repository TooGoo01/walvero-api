using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Implementations.Main;
using Loyalty.DataAccess.UnitOfWorks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IProductRepository _productRepository;

        public ProductService(IProductRepository productRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _productRepository = productRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public Task<ServiceResult> ActiveProduct(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> AddProduct(AddProductDto product)
        {
            var request = _mapper.Map<Product>(product);
            request.IsActive = true;
            await _unitOfWork.Repository<Product>().AddAsync(request);
            _unitOfWork.Commit();
            var response = _mapper.Map<GetProductDto>(request);
            return new ServiceResult(true, response.Id);
        }

        public Task<ServiceResult> DeactiveProduct(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> DeleteProduct(int id)
        {
            var about = await _productRepository.GetById(id);
            if (about != null)
            {
                about.IsActive = false;
                _unitOfWork.Repository<Product>().Update(about);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }

        public Task<ServiceResult> GetActiveProducts()
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult> GetDeactiveProducts()
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> GetProduct(int id)
        {
            try
            {
                var result = await _productRepository.GetById(id);
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = _mapper.Map<GetProductDto>(result);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public Task<ServiceResult> GetProductBySlug(string slug)
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult> GetProductForUpdate(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> GetProducts()
        {
            var partners = await _productRepository.GetAll();
            if (partners != null)
            {
                var response = _mapper.Map<ICollection<GetProductDto>>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }

        public Task<ServiceResult> UpdateProduct(AddProductDto addProductDto,int id)
        {
            throw new NotImplementedException();
        }
    }
}
