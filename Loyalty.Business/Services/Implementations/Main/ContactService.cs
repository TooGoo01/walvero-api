using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class ContactService : IContactService
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IContactRepository _contactRepository;
        private readonly IMailService _mailService;

        public ContactService(IUnitOfWork unitOfWork, IMapper mapper, IContactRepository contactRepository)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _contactRepository = contactRepository;
        }

        public async Task<ServiceResult> AddContact(AddContactDto contact)
        {
            var request = _mapper.Map<Contact>(contact);
            //await _mailService.SendMailAsync("info@maxoilltd.com", @contact.Subject, $"{contact.Message}");
            await _unitOfWork.Repository<Contact>().AddAsync(request);
            _unitOfWork.Commit();
            return new ServiceResult(true, request);
        }

        public async Task<ServiceResult> DeleteContactById(int contactId)
        {
            var meet = _unitOfWork.Repository<Contact>().CheckExist(x => x.Id == contactId);

            if (meet == false)
                return new ServiceResult(false, "Contact not found");

            _unitOfWork.Repository<Contact>().Delete(x => x.Id == contactId);
            _unitOfWork.Commit();
            return new ServiceResult(true);
        }

        public async Task<ServiceResult> GetContact(int contactId)
        {
            try
            {
                var result = await _contactRepository.GetContact(contactId);
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = _mapper.Map<GetContactDto>(result);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> GetContacts()
        {
            var meets = await _unitOfWork.Repository<Contact>().GetAllAsync();

            if (meets == null)
                return new ServiceResult(false);
            var response = _mapper.Map<IEnumerable<GetContactDto>>(meets);
            return new ServiceResult(true, response);
        }
    }
}
