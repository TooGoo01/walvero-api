using AutoMapper;
using Azure.Core;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Implementations.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class TenantService : ITenantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ITenantRepository _tenantRepository;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITenantProvider _tenantProvider;

        public TenantService(
            ITenantRepository tenantRepository,
            IMapper mapper,
            IUnitOfWork unitOfWork,
            LoyaltyWalletDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            ITenantProvider tenantProvider)
        {
            _tenantRepository = tenantRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _dbContext = dbContext;
            _userManager = userManager;
            _tenantProvider = tenantProvider;
        }

        public async Task<ServiceResult> Add(AddTenantDto Dto)
        {
            // Validation: Şəhər seçimi məcburidir
            if (!Dto.cityId.HasValue || Dto.cityId.Value <= 0)
            {
                return new ServiceResult(false, "Şəhər seçimi məcburidir");
            }

            var request = _mapper.Map<Tenant>(Dto);
            request.IsActive = true;
            await _unitOfWork.Repository<Tenant>().AddAsync(request);
            _unitOfWork.Commit();

            // Create TenantLocation if location data is provided
            if (Dto.countryId.HasValue || Dto.cityId.HasValue || Dto.timeZoneId.HasValue)
            {
                var tenantLocation = new TenantLocation
                {
                    tenantId = request.Id,
                    countryId = Dto.countryId,
                    cityId = Dto.cityId,
                    timeZoneId = Dto.timeZoneId
                };
                await _unitOfWork.Repository<TenantLocation>().AddAsync(tenantLocation);
                _unitOfWork.Commit();
                request.tenantLocationId = tenantLocation.Id;
                _unitOfWork.Repository<Tenant>().Update(request);
                _unitOfWork.Commit();
            }

            // Reload tenant with TenantLocation for proper mapping
            var tenantWithLocation = await _tenantRepository.GetById(request.Id);
            var response = _mapper.Map<GetTenantDto>(tenantWithLocation);
            return new ServiceResult(true, response.Id);
        }

        public Task<ServiceResult> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> Get(int id)
        {
            var respose = await _tenantRepository.GetById(id);
            if (respose == null)
                return new ServiceResult(false, "Tenant not found");
            var response = _mapper.Map<GetTenantDto>(respose);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetActive()
        {
            var respose = await _tenantRepository.GetActive();

            var response = _mapper.Map<List<GetTenantDto>>(respose);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetActiveUser(int currentPage, int pageSize, TenantSearchDto model)
        {
            if (pageSize > 100)
                pageSize = 100;
            
            // Resolve user context for personalized "IsCustomer":
            // 1) Prefer authenticated JWT user id (ClaimTypes.NameIdentifier via ITenantProvider)
            // 2) If not authenticated but client passed phone in model.userName, resolve ApplicationUser by that phone
            //    and compute "IsCustomer" for that user (prevents "isCustomer=true but MyCards empty" drift).
            int? applicationUserId = null;

            var currentUserIdStr = _tenantProvider.CurrentUserId;
            if (!string.IsNullOrWhiteSpace(currentUserIdStr) && int.TryParse(currentUserIdStr, out var parsedUserId))
            {
                applicationUserId = parsedUserId;
            }
            else if (!string.IsNullOrWhiteSpace(model?.userName))
            {
                var phoneOrUserName = model.userName.Trim();

                // Most of the system uses phone as UserName; still, check both PhoneNumber and UserName to be safe.
                var resolvedUser = await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserName == phoneOrUserName || u.PhoneNumber == phoneOrUserName);

                if (resolvedUser != null)
                    applicationUserId = resolvedUser.Id;
            }

            // OPTIMIZATION: Get IQueryable and count BEFORE pagination
            // Backward compatibility: if unauthenticated, fall back to the existing phone-based discover logic.
            var query = applicationUserId.HasValue
                ? _tenantRepository.GetActiveWithApplicationUser(
                    model.Name,
                    applicationUserId.Value,
                    model.categoryId,
                    model.countryId,
                    model.cityId)
                : _tenantRepository.GetActiveWithUser(
                    model.Name,
                    model.userName,
                    model.categoryId,
                    model.countryId,
                    model.cityId);
            
            // Execute count query first (single database call)
            var totalCount = await query.CountAsync();
            
            // Apply pagination at database level and materialize
            var page = await query
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var mappedTenants = page.Select(tenant =>
            {
                // When authenticated: repo already filtered Customers+Cards for the current user and active cards only.
                // When unauthenticated: repo filtered by phone number (legacy).
                var isCustomer = tenant.Customers != null &&
                                 tenant.Customers.Any(c => c.LoyaltyCards != null &&
                                                           c.LoyaltyCards.Any(lc => lc.IsActive && lc.Status == CardStatus.Active));

                var loyaltyPrograms = tenant.LoyaltyPrograms?
                    .Where(lp => lp.IsActive)
                    .OrderBy(lp => lp.Id)
                    .Select(lp => new TenantDiscoverProgramDto
                    {
                        Id = lp.Id,
                        ProgramName = lp.ProgramName,
                        Description = lp.Description,
                        CardTemplate = lp.CardTemplate == null
                            ? null
                            : new TenantDiscoverCardTemplateDto
                            {
                                BackgroundColor = lp.CardTemplate.BackgroundColor,
                                ForegroundColor = lp.CardTemplate.ForegroundColor,
                                LogoUrl = lp.CardTemplate.LogoUrl,
                                GoogleLogoUrl = lp.CardTemplate.GoogleLogoUrl
                            }
                    })
                    .ToList() ?? new List<TenantDiscoverProgramDto>();

                return new TenantDiscoverDto
                {
                    Id = tenant.Id,
                    CompanyName = tenant.CompanyName,
                    IsCustomer = isCustomer,
                    Category = tenant.Category == null
                        ? null
                        : new TenantDiscoverCategoryDto { EnName = tenant.Category.EnName },
                    LoyaltyPrograms = loyaltyPrograms
                };
            });

            SearchResult<IEnumerable<TenantDiscoverDto>> response = new SearchResult<IEnumerable<TenantDiscoverDto>>()
            {
                Value = mappedTenants,
                DataCount = totalCount,
                CurrentPage = currentPage,
                PageSize = pageSize
            };

            return new ServiceResult(true, response);
        }

      

        public async Task<ServiceResult> GetAll()
        
        {
            var respose = await _tenantRepository.GetAll();
            
            var response = _mapper.Map<List<GetTenantDto>>(respose);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetById(int id)
        {
            var respose = await _tenantRepository.GetById(id);
            var response = _mapper.Map<GetTenantDto>(respose);
            return new ServiceResult(true, response);
        }

        public Task<ServiceResult> GetLast()
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> HasAnyRoleAsync(int userId, int tenantId, string[] roles)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(x=>x.Id ==userId);
            var roleList = await _userManager.GetRolesAsync(user);
            if (user is null)
                return new ServiceResult(false, "User not found");

            // Case-insensitive müqayisə üçün set
            var requested = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);

            // 2) User-ın Identity rollarını oxu
            var userRoles = await _userManager.GetRolesAsync(user);
            var tenant = await _tenantRepository.GetById(tenantId);
         
            bool hasAnyRole = userRoles.Any(ur => requested.Contains(ur));
            bool isTenantAdmin = tenant.Admins.Any(a => a.Id == userId);
            // 5) Şərt: (istənilən rol) VƏYA (tenant admin)
            bool ok = hasAnyRole && isTenantAdmin;
            if(ok==true)
             return new ServiceResult(true, ok);
            else
            {
                return new ServiceResult(false, ok);
            }
        }

        public async Task<ServiceResult> Update(UpdateTenantDto dto)
        {
            // Validation: Şəhər seçimi məcburidir
            if (!dto.cityId.HasValue || dto.cityId.Value <= 0)
            {
                return new ServiceResult(false, "Şəhər seçimi məcburidir");
            }

            var category = await _tenantRepository.GetById(dto.Id);
            if (category != null)
            {
                if (dto.CompanyName != null)
                {
                    category.CompanyName = dto.CompanyName;
                }
                if (dto.Subdomain != null)
                {
                    category.Subdomain = dto.Subdomain;
                }
                if (dto.ContactEmail != null)
                {
                    category.ContactEmail = dto.ContactEmail;
                }
                if (dto.ContactPhone != null)
                {
                    category.ContactPhone = dto.ContactPhone;
                }
                if (dto.LocationUrl != null)
                {
                    category.LocationUrl = dto.LocationUrl;
                }
                if (dto.InstagramUrl != null)
                {
                    category.InstagramUrl = dto.InstagramUrl;
                }
                if (dto.FacebookUrl != null)
                {
                    category.FacebookUrl = dto.FacebookUrl;
                }
                if (dto.WhatsAppUrl != null)
                {
                    category.WhatsAppUrl = dto.WhatsAppUrl;
                }
                if (dto.TelegramUrl != null)
                {
                    category.TeleqramUrl = dto.TelegramUrl;
                }
                category.ShowOnHome = dto.ShowOnHome;
                category.categoryId = dto.categoryId;
                category.RequireConfirmation = dto.RequireConfirmation ? true : false;
                if (dto.ConfirmationTtlMinutes != null)
                {
                    category.ConfirmationTtlMinutes = dto.ConfirmationTtlMinutes;
                }

                // Handle TenantLocation
                if (dto.countryId.HasValue || dto.cityId.HasValue || dto.timeZoneId.HasValue)
                {
                    TenantLocation tenantLocation = null;
                    if (category.tenantLocationId.HasValue)
                    {
                        tenantLocation = await _unitOfWork.Repository<TenantLocation>().GetByIdAsync(category.tenantLocationId.Value);
                    }

                    if (tenantLocation == null)
                    {
                        tenantLocation = new TenantLocation
                        {
                            tenantId = category.Id,
                            countryId = dto.countryId,
                            cityId = dto.cityId,
                            timeZoneId = dto.timeZoneId
                        };
                        await _unitOfWork.Repository<TenantLocation>().AddAsync(tenantLocation);
                        _unitOfWork.Commit();
                        category.tenantLocationId = tenantLocation.Id;
                    }
                    else
                    {
                        tenantLocation.countryId = dto.countryId;
                        tenantLocation.cityId = dto.cityId;
                        tenantLocation.timeZoneId = dto.timeZoneId;
                        _unitOfWork.Repository<TenantLocation>().Update(tenantLocation);
                    }
                }
                var users = await _dbContext.Users.ToListAsync();
                var selectedUser = users.Where(d => dto.admins.Contains(d.Id)).ToList();
                category.Admins = selectedUser;
                //if (!categoryDto.EnName.IsNullOrEmpty())
                //{
                //    category.EnName = categoryDto.EnName;
                //}
                //if (!categoryDto.RuName.IsNullOrEmpty())
                //{
                //    category.RuName = categoryDto.RuName;
                //}

                //if (!categoryDto.AzDescription.IsNullOrEmpty())
                //{
                //    category.AzDescription = categoryDto.AzDescription;
                //}
                //if (!categoryDto.EnDescription.IsNullOrEmpty())
                //{
                //    category.EnDescription = categoryDto.EnDescription;
                //}
                //if (!categoryDto.RuDescription.IsNullOrEmpty())
                //{
                //    category.RuDescription = categoryDto.RuDescription;
                //}

                _unitOfWork.Repository<Tenant>().Update(category);
                _unitOfWork.Commit();

                // Reload tenant with TenantLocation for proper mapping
                var updatedTenant = await _tenantRepository.GetById(category.Id);
                var response = _mapper.Map<GetTenantDto>(updatedTenant);
                return new ServiceResult(true, response.Id);
            }
            return new ServiceResult(false);
        }
        public async Task<IReadOnlyList<HomeTenantDto>> GetHomeTenantsAsync(int? countryId = null, int? cityId = null, CancellationToken ct = default)
        {
            var projections = await _tenantRepository.ListHomeTenantsAsync(countryId, cityId, ct);

            // Map from repository projection to service DTO
            return projections.Select(p => new HomeTenantDto
            {
                TenantId = p.TenantId,
                TenantName = p.TenantName,
                TenantProgramName = p.TenantProgramName,
                GoogleLogoUrl = p.GoogleLogoUrl,
                RewardDescription = p.RewardDescription
            }).ToList();
        }
    }
}
