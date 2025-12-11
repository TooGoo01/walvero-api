using AutoMapper;
using Microsoft.IdentityModel.Tokens;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
namespace Loyalty.Business.Services.Implementations.Main
{
    public class CategoryService : ICategoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICategoryRepository _categoryRepository;

        public CategoryService(ICategoryRepository categoryRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public async Task<ServiceResult> AddCategory(AddCategoryDto category)
        {
            var request = _mapper.Map<Category>(category);
            request.IsActive = true;
            await _unitOfWork.Repository<Category>().AddAsync(request);
            _unitOfWork.Commit();
            var response = _mapper.Map<GetCategoryDto>(request);
            return new ServiceResult(true, response.Id);
        }

        public async Task<ServiceResult> DeleteCategory(int id)
        {
            var category = await _categoryRepository.GetCategory(id);
            if (category != null)
            {
                category.IsActive = false;
                _unitOfWork.Repository<Category>().Update(category);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }
        public async Task<ServiceResult> DeactiveCategory(int id)
        {
            var category = await _categoryRepository.GetDeactiveCategory(id);
           
            return new ServiceResult(false);
        }
        public async Task<ServiceResult> ActiveCategory(int id)
        {
            var category = await _categoryRepository.GetActiveCategory(id);
         
            return new ServiceResult(false, category);
        }
        public async Task<ServiceResult> GetActiveCategories()
        {
            var categories = await _categoryRepository.GetActiveCategories();
            if (categories != null)
            {
                var response = _mapper.Map<ICollection<GetCategoryIdNameDto>>(categories);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }
        public async Task<ServiceResult> GetDeactiveCategories()
        {
            var categories = await _categoryRepository.GetDeActiveCategories();
            if (categories != null)
            {
                var response = _mapper.Map<ICollection<GetCategoryDto>>(categories);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false, categories);
        }

        public async Task<ServiceResult> GetActiveCategoriesIdName()
        {
            var categories = await _categoryRepository.GetActiveCategoriesIdName();
            if (categories != null)
            {
                var response = _mapper.Map<ICollection<GetCategoryIdNameDto>>(categories);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetCategories()
        {
            var categories = await _categoryRepository.GetAllCategories();
            if (categories != null)
            {
                var response = _mapper.Map<ICollection<GetCategoryIdNameDto>>(categories);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetCategory(int id)
        {
            try
            {
                var result = await _categoryRepository.GetCategory(id);
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = _mapper.Map<GetCategoryDto>(result);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> UpdateCategory(AddCategoryDto categoryDto, int id)
        {
            var category = await _categoryRepository.GetCategory(id);
            if (category != null)
            {
                //if (!categoryDto.AzName.IsNullOrEmpty())
                //{
                //    category.AzName = categoryDto.AzName;
                //}
                //if (!categoryDto.EnName.IsNullOrEmpty())
                //{
                //    category.EnName = categoryDto.EnName;
                //}
                //if (!categoryDto.RuName.IsNullOrEmpty())
                //{
                //    category.RuName = categoryDto.RuName;
                //}

                //if (!categoryDto.AzDescription.IsNullOrEmpty())
                //{
                //    category.AzDescription = categoryDto.AzDescription;
                //}
                //if (!categoryDto.EnDescription.IsNullOrEmpty())
                //{
                //    category.EnDescription = categoryDto.EnDescription;
                //}
                //if (!categoryDto.RuDescription.IsNullOrEmpty())
                //{
                //    category.RuDescription = categoryDto.RuDescription;
                //}

                _unitOfWork.Repository<Category>().Update(category);
                _unitOfWork.Commit();
                var response = _mapper.Map<GetCategoryDto>(category);
                return new ServiceResult(true, response.Id);
            }
            return new ServiceResult(false);
        }
    }
}
