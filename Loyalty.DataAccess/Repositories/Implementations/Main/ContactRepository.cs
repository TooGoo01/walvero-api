using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;


namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class ContactRepository : GenericRepository<Contact>, IContactRepository
    {
        public ContactRepository(LoyaltyWalletDbContext educationDbContext) : base(educationDbContext)
        {

        }
        public async Task<Contact> GetContact(int contactId)
        {
            return await GetAsQueryable().FirstOrDefaultAsync(x => x.Id == contactId);
        }
    }
}
