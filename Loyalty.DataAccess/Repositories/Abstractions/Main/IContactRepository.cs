using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IContactRepository
    {
        Task<Contact> GetContact(int contactId);
    }
}
