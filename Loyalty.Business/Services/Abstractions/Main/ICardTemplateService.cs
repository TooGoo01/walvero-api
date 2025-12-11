using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ICardTemplateService
    {
        Task<ServiceResult> Add(AddCardTemplateDto Dto);
        Task<ServiceResult> AddOrUpdateAsync(AddCardTemplateDto dto, CancellationToken ct = default);
        Task<ServiceResult> Get(int id);
        Task<ServiceResult> GetAll();
        Task<ServiceResult> GetLast();
        Task<ServiceResult> Update(AddCardTemplateDto dto, int id);
        Task<ServiceResult> Delete(int id);
    }
}
