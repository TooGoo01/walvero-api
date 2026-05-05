using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Get.Main.Branch;
using Loyalty.Business.Dtos.Get.Main.Room;
using Loyalty.Business.Dtos.Get.Users;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Dtos.Post.Main.Branch;
using Loyalty.Business.Dtos.Post.Main.Room;
using Loyalty.Business.Dtos.Post.Users;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Models;
using LoyaltyWalletApp.Business.Dtos.Get.Main;
using System.Diagnostics.Metrics;
using BranchEntity = Loyalty.DataAccess.Entities.Main.Branch;
using RoomEntity = Loyalty.DataAccess.Entities.Main.Room;
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

            CreateMap<AddSalesRepVisitDto, SalesRepVisit>()
                .ForMember(d => d.Status, opt => opt.MapFrom(s => (SalesVisitStatus)s.Status))
                .ForMember(d => d.Id, opt => opt.Ignore())
                .ForMember(d => d.RegDate, opt => opt.Ignore())
                .ForMember(d => d.IsActive, opt => opt.Ignore());
            CreateMap<UpdateSalesRepVisitDto, SalesRepVisit>()
                .ForMember(d => d.Status, opt => opt.MapFrom(s => (SalesVisitStatus)s.Status))
                .ForMember(d => d.Id, opt => opt.Ignore())
                .ForMember(d => d.RegDate, opt => opt.Ignore())
                .ForMember(d => d.IsActive, opt => opt.Ignore());
            CreateMap<SalesRepVisit, GetSalesRepVisitDto>()
                .ForMember(d => d.Status, opt => opt.MapFrom(s => (int)s.Status))
                .ForMember(d => d.StatusName, opt => opt.MapFrom(s => s.Status.ToString()));

        

            CreateMap<Partner, AddPartnerDto>().ReverseMap();
            CreateMap<Partner, GetPartnerDto>().ReverseMap();

            CreateMap<Tenant, AddTenantDto>()
                .ForMember(d => d.AcceptAnonymous, opt => opt.MapFrom(s => s.AcceptAnonymus))
                .ReverseMap()
                .ForMember(d => d.AcceptAnonymus, opt => opt.MapFrom(s => s.AcceptAnonymous))
                .ForMember(d => d.TeleqramUrl, opt => opt.MapFrom(s => s.TelegramUrl));
            CreateMap<Tenant, GetTenantDto>()
                .ForMember(d => d.AcceptAnonymous, opt => opt.MapFrom(s => s.AcceptAnonymus))
                .ForMember(d => d.TelegramUrl, opt => opt.MapFrom(s => s.TeleqramUrl))
                .ForMember(d => d.CreatedAt, opt => opt.MapFrom(s => s.RegDate))
                .ForMember(d => d.IsCustomer,
                    opt => opt.MapFrom(s => s.Customers != null &&
                                             s.Customers.Any(c => c.LoyaltyCards != null &&
                                                                  c.LoyaltyCards.Any(lc => lc.IsActive))))
                .ForMember(d => d.AcceptAnonymous, opt => opt.MapFrom(s => s.AcceptAnonymus))
                .ForMember(d => d.TelegramUrl, opt => opt.MapFrom(s => s.TeleqramUrl));

            CreateMap<TenantBranding, AddTenantBrandingDto>().ReverseMap();
            CreateMap<TenantBranding, GetTenantBrandingDto>().ReverseMap();

            CreateMap<WalletCredential, AddWalletCredentialDto>().ReverseMap();
            CreateMap<WalletCredential, GetWalletCredentialDto>().ReverseMap();
            CreateMap<Tenant, WalletCredentialTenantDto>();

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
                                                                  c.LoyaltyCards.Any(lc => lc.IsActive))))
                .ForMember(d => d.AcceptAnonymous, opt => opt.MapFrom(s => s.AcceptAnonymus))
                .ForMember(d => d.TelegramUrl, opt => opt.MapFrom(s => s.TeleqramUrl));
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
            CreateMap<MenuCategory, GetMenuCategorySummaryDto>(); // No MenuItems - used in GetMenuItemDto to avoid JSON cycle

            CreateMap<MenuItem, AddMenuItemDto>().ReverseMap();
            CreateMap<MenuItem, UpdateMenuItemDto>().ReverseMap();
            CreateMap<MenuItem, GetMenuItemDto>().ReverseMap();

            CreateMap<Transaction, GetTransactionDto>()
                .ForMember(d => d.CustomerName,
                    opt => opt.MapFrom(s => s.LoyaltyCard != null && s.LoyaltyCard.Customer != null
                        ? s.LoyaltyCard.Customer.FirstName + " " + s.LoyaltyCard.Customer.LastName
                        : null))
                .ForMember(d => d.CardNumber,
                    opt => opt.MapFrom(s => s.LoyaltyCard != null ? s.LoyaltyCard.CardNumber : null))
                .ForMember(d => d.CreatedAt,
                    opt => opt.MapFrom(s => s.RegDate));

            CreateMap<MenuItemImage, AddMenuItemImageDto>().ReverseMap();
            CreateMap<MenuItemImage, GetMenuItemImageDto>().ReverseMap();

            CreateMap<RestorantWifiInformation, AddRestorantWifiInformationDto>().ReverseMap();
            CreateMap<RestorantWifiInformation, UpdateRestorantWifiInformationDto>().ReverseMap();
            CreateMap<RestorantWifiInformation, GetRestorantWifiInformationDto>().ReverseMap();

            CreateMap<TenantLink, AddTenantLinkDto>().ReverseMap();
            CreateMap<TenantLink, UpdateTenantLinkDto>().ReverseMap();
            CreateMap<TenantLink, GetTenantLinkDto>().ReverseMap();

            CreateMap<Transaction, GetTransactionDto>()
                .ForMember(d => d.Status, opt => opt.MapFrom(s => (int)s.Status))
                .ForMember(d => d.CustomerName, opt => opt.MapFrom(s =>
                    s.LoyaltyCard != null && s.LoyaltyCard.Customer != null
                        ? (s.LoyaltyCard.Customer.FirstName + " " + s.LoyaltyCard.Customer.LastName).Trim()
                        : null))
                .ForMember(d => d.CardNumber, opt => opt.MapFrom(s =>
                    s.LoyaltyCard != null ? s.LoyaltyCard.CardNumber : null))
                .ForMember(d => d.CreatedAt, opt => opt.MapFrom(s => s.RegDate));

            #endregion

            #region Branch & Room
            CreateMap<BranchEntity, GetBranchDto>()
                .ForMember(d => d.StaffCount, opt => opt.Ignore())
                .ForMember(d => d.DistanceKm, opt => opt.Ignore());
            CreateMap<BranchEntity, BranchSummaryDto>();

            CreateMap<CreateBranchDto, BranchEntity>()
                .ForMember(d => d.Id, opt => opt.Ignore())
                .ForMember(d => d.RegDate, opt => opt.Ignore())
                .ForMember(d => d.IsActive, opt => opt.Ignore())
                .ForMember(d => d.RegUserId, opt => opt.Ignore())
                .ForMember(d => d.EditDate, opt => opt.Ignore())
                .ForMember(d => d.EditUserId, opt => opt.Ignore())
                .ForMember(d => d.Tenant, opt => opt.Ignore())
                .ForMember(d => d.TenantId, opt => opt.Ignore())
                .ForMember(d => d.Rooms, opt => opt.Ignore())
                .ForMember(d => d.RadiusMeters, opt => opt.MapFrom(s => s.RadiusMeters ?? 100))
                .ForMember(d => d.IsMainBranch, opt => opt.MapFrom(s => s.IsMainBranch ?? false));

            CreateMap<RoomEntity, GetRoomDto>()
                .ForMember(d => d.MediaCount, opt => opt.MapFrom(s => s.Files != null ? s.Files.Count(f => f.IsActive) : 0));
            CreateMap<RoomFile, GetRoomFileDto>()
                .ForMember(d => d.Url, opt => opt.MapFrom(s => s.Path))
                .ForMember(d => d.ThumbnailUrl, opt => opt.MapFrom(s => s.ThumbnailPath));

            CreateMap<CreateRoomDto, RoomEntity>()
                .ForMember(d => d.Id, opt => opt.Ignore())
                .ForMember(d => d.RegDate, opt => opt.Ignore())
                .ForMember(d => d.IsActive, opt => opt.Ignore())
                .ForMember(d => d.RegUserId, opt => opt.Ignore())
                .ForMember(d => d.EditDate, opt => opt.Ignore())
                .ForMember(d => d.EditUserId, opt => opt.Ignore())
                .ForMember(d => d.Branch, opt => opt.Ignore())
                .ForMember(d => d.Files, opt => opt.Ignore())
                .ForMember(d => d.OrderIndex, opt => opt.MapFrom(s => s.OrderIndex ?? 0));
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
