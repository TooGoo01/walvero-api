using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Get.Users;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Dtos.Post.Users;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using LoyaltyWalletApp.Business.Dtos.Get.Main;


namespace Loyalty.Business.Mappings
{
    public class MapProfile : Profile
    {
        public MapProfile()
        {

            #region Main
            CreateMap<Contact, AddContactDto>().ReverseMap();
            CreateMap<Contact, GetContactDto>().ReverseMap();

        

            CreateMap<Partner, AddPartnerDto>().ReverseMap();
            CreateMap<Partner, GetPartnerDto>().ReverseMap();

            CreateMap<Tenant, AddTenantDto>().ReverseMap();
            CreateMap<Tenant, GetTenantDto>()
                .ForMember(d => d.IsCustomer,
                    opt => opt.MapFrom(s => s.Customers != null && 
                                             s.Customers.Any(c => c.LoyaltyCards != null && 
                                                                  c.LoyaltyCards.Any(lc => lc.IsActive))));

            CreateMap<TenantBranding, AddTenantBrandingDto>().ReverseMap();
            CreateMap<TenantBranding, GetTenantBrandingDto>().ReverseMap();

            CreateMap<WalletCredential, AddWalletCredentialDto>().ReverseMap();
            CreateMap<WalletCredential, GetWalletCredentialDto>().ReverseMap();

            CreateMap<LoyaltyProgram, AddLoyaltyProgramDto>().ReverseMap();
            CreateMap<LoyaltyProgram, GetLoyaltyProgramDto>().ReverseMap();

            CreateMap<CardTemplate, AddCardTemplateDto>().ReverseMap();
            CreateMap<CardTemplate, GetCardTemplateDto>().ReverseMap();

            CreateMap<AppVersionConfig, AddAppVersion>().ReverseMap();
            CreateMap<AppVersionConfig, GetAppVersionDto>().ReverseMap();

            CreateMap<Customer, AddCustomerDto>().ReverseMap();
            CreateMap<Customer, GetCustomerDto>().ReverseMap();
            CreateMap<Customer, GetCustomerIdNameDto>().ReverseMap();

            CreateMap<Category, AddCategoryDto>().ReverseMap();
            CreateMap<Category, GetCategoryDto>().ReverseMap();
            CreateMap<Category, GetCategoryIdNameDto>().ReverseMap();

            CreateMap<LoyaltyCard, AddLoyaltyCardDto>().ReverseMap();
            CreateMap<LoyaltyCard, GetLoyaltyCardDto>().ReverseMap();
            CreateMap<Icon, IconDto>().ReverseMap();

            CreateMap<StoreFeedback, GetStoreFeedbackDto>().ReverseMap();
            CreateMap<StoreFeedbackReply, GetStoreFeedbackReplyDto>().ReverseMap();
            CreateMap<Notification, GetNotificationDto>().ReverseMap();
            CreateMap<Tenant, GetTenantDto>()
                .ForMember(d => d.IsCustomer,
                    opt => opt.MapFrom(s => s.Customers != null && 
                                             s.Customers.Any(c => c.LoyaltyCards != null && 
                                                                  c.LoyaltyCards.Any(lc => lc.IsActive))));



            #endregion




            #region User
            CreateMap<ApplicationUser, ApplicationUserDto>().ReverseMap();
            CreateMap<RegisterUserDto, ApplicationUser>().ReverseMap();
            CreateMap<ApplicationUser, RegisterUserDto>().ReverseMap();
            CreateMap<ApplicationUser, LoginUserDto>().ReverseMap();
            CreateMap<LoginUserDto, ApplicationUser>().ReverseMap();
            CreateMap<ApplicationRole, ApplicationRoleDto>().ReverseMap();
            #endregion
        }
    }
}
