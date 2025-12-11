using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IProductService
    {
        #region Get
        Task<ServiceResult> GetProduct(int id);
        Task<ServiceResult> GetProductBySlug(string slug);
        Task<ServiceResult> GetProductForUpdate(int id);
        Task<ServiceResult> GetProducts();
        Task<ServiceResult> GetDeactiveProducts();
        Task<ServiceResult> GetActiveProducts();
        #endregion

        #region Post
        Task<ServiceResult> AddProduct(AddProductDto product);
        Task<ServiceResult> UpdateProduct(AddProductDto addProductDto, int id);
        Task<ServiceResult> DeleteProduct(int id);
        Task<ServiceResult> DeactiveProduct(int id);
        Task<ServiceResult> ActiveProduct(int id);
        #endregion
    }
}
