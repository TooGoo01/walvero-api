using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ICardTemplateRepository : IGenericRepository<CardTemplate>
    {
        Task<CardTemplate?> GetByLoyaltyProgramIdAsync(int programId, CancellationToken cancellationToken = default);
        Task<IEnumerable<CardTemplate>> GetByTemplateTypeAsync(CardTemplateType type, CancellationToken cancellationToken = default);
        Task<CardTemplate> GetById(int id);
    }
}
