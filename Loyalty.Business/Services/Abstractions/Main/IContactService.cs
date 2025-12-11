using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IContactService
    {
        Task<ServiceResult> AddContact(AddContactDto contact);
        Task<ServiceResult> DeleteContactById(int contactId);
        Task<ServiceResult> GetContact(int contactId);
        Task<ServiceResult> GetContacts();
    }
}
