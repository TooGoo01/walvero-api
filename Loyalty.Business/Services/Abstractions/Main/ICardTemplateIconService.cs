using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ICardTemplateIconService
    {
        Task<ServiceResult> AddRangeAsync(AddCardTemplateDto dto, int templateId, CancellationToken ct);
        Task<ServiceResult> UpdateRangeAsync(AddCardTemplateDto addCardTemplateDto, int aboutId);
    }
}
