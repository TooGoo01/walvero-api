using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Get.Users;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Dtos.Post.Users;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Models;
using LoyaltyWalletApp.Business.Dtos.Get.Main;
using System.Diagnostics.Metrics;
using TimeZone = Loyalty.DataAccess.Entities.Main.TimeZone;


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
            CreateMap<Customer, GetCustomerDto>()
                .AfterMap((src, dest) =>
                {
                    // Set Currency from first active card's CardTemplate
                    var firstCard = src.LoyaltyCards?.FirstOrDefault(c => c.IsActive);
                    if (firstCard?.LoyaltyProgram?.CardTemplate != null)
                    {
                        dest.Currency = firstCard.LoyaltyProgram.CardTemplate.Currency;
                    }
                })
                .ReverseMap();
            CreateMap<Customer, GetCustomerIdNameDto>().ReverseMap();

            CreateMap<Category, AddCategoryDto>().ReverseMap();
            CreateMap<Category, GetCategoryDto>().ReverseMap();
            CreateMap<Category, GetCategoryIdNameDto>().ReverseMap();

            CreateMap<LoyaltyCard, AddLoyaltyCardDto>().ReverseMap();
            CreateMap<LoyaltyCard, GetLoyaltyCardDto>()
                .AfterMap((src, dest) =>
                {
                    // Set Currency from CardTemplate
                    if (src.LoyaltyProgram?.CardTemplate != null)
                    {
                        dest.Currency = src.LoyaltyProgram.CardTemplate.Currency;
                    }
                })
                .ReverseMap();
            CreateMap<Icon, IconDto>().ReverseMap();

            CreateMap<StoreFeedback, GetStoreFeedbackDto>().ReverseMap();
            CreateMap<StoreFeedbackReply, GetStoreFeedbackReplyDto>().ReverseMap();
            CreateMap<Notification, GetNotificationDto>().ReverseMap();
            CreateMap<Tenant, GetTenantDto>()
                .ForMember(d => d.IsCustomer,
                    opt => opt.MapFrom(s => s.Customers != null && 
                                             s.Customers.Any(c => c.LoyaltyCards != null && 
                                                                  c.LoyaltyCards.Any(lc => lc.IsActive))));
            CreateMap<Offer, AddOfferDto>().ReverseMap();
            CreateMap<Offer, GetOfferDto>().ReverseMap();

            CreateMap<Story, AddStoryDto>().ReverseMap();
            CreateMap<Story, GetStoryDto>().ReverseMap();

            // Home page projections mapping
            CreateMap<HomeTenantProjection, HomeTenantDto>().ReverseMap();
            CreateMap<HomeStoryProjection, HomeStoryDto>().ReverseMap();
            CreateMap<HomeOfferProjection, HomeOfferDto>().ReverseMap();
            CreateMap<Country, AddCountryDto>().ReverseMap();
            CreateMap<Country, GetCountryDto>().ReverseMap();

            CreateMap<City, AddCityDto>().ReverseMap();
            CreateMap<City, GetCityDto>().ReverseMap();

            CreateMap<TimeZone, AddTimeZoneDto>().ReverseMap();
            CreateMap<TimeZone, GetTimeZoneDto>().ReverseMap();

            CreateMap<TenantLocation, GetTenantLocationDto>()
                  .ForMember(dest => dest.CountryName, opt => opt.MapFrom(src => src.Country != null ? src.Country.AzName : null))
                  .ForMember(dest => dest.CityName, opt => opt.MapFrom(src => src.City != null ? src.City.AzName : null))
                  .ForMember(dest => dest.TimeZoneName, opt => opt.MapFrom(src => src.TimeZone != null ? src.TimeZone.Name : null));

            // Menu related mappings
            CreateMap<MenuCategory, AddMenuCategoryDto>().ReverseMap();
            CreateMap<MenuCategory, UpdateMenuCategoryDto>().ReverseMap();
            CreateMap<MenuCategory, GetMenuCategoryDto>().ReverseMap();

            CreateMap<MenuItem, AddMenuItemDto>().ReverseMap();
            CreateMap<MenuItem, UpdateMenuItemDto>().ReverseMap();
            CreateMap<MenuItem, GetMenuItemDto>().ReverseMap();

            CreateMap<MenuItemImage, AddMenuItemImageDto>().ReverseMap();
            CreateMap<MenuItemImage, GetMenuItemImageDto>().ReverseMap();

            CreateMap<RestorantWifiInformation, AddRestorantWifiInformationDto>().ReverseMap();
            CreateMap<RestorantWifiInformation, UpdateRestorantWifiInformationDto>().ReverseMap();
            CreateMap<RestorantWifiInformation, GetRestorantWifiInformationDto>().ReverseMap();


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
