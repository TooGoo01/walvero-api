using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class AboutRepository : GenericRepository<About>, IAboutRepository
    {
        public AboutRepository(LoyaltyWalletDbContext educationDbContext) : base(educationDbContext)
        {

        }

        public async Task<IEnumerable<About>> GetAbouts()
        {
            return await GetAsQueryable().ToListAsync();
        }

        public async Task<IEnumerable<About>> GetAllAbouts()
        {
            return await GetAsQueryable()
              .ToListAsync();
        }

        public async Task<About> GetAbout(int aboutId)
        {
            return await GetAsQueryable()
               
                .FirstOrDefaultAsync(x => x.Id == aboutId);
        }

        public async Task<IEnumerable<About>> GetActiveAbouts()
        {
            return await GetAsQueryable()
              
                .Where(x => x.IsActive == true)
                .ToListAsync();
        }

   

        public async Task<About> GetLastAbout()
        {
            return await GetAsQueryable()
                .OrderBy(x => x.Id)
                .Include(x=>x.AboutFiles)
                 .Where(x => x.IsActive == true)
                 .LastOrDefaultAsync();
        }

        public async Task<About> GetAbout()
        {
            return await GetAsQueryable().FirstOrDefaultAsync();
        }
    }
}
