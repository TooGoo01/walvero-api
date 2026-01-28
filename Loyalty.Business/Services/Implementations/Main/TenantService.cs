using AutoMapper;
using Azure.Core;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
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
        public TenantService(ITenantRepository tenantRepository, IMapper mapper, IUnitOfWork unitOfWork, LoyaltyWalletDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            _tenantRepository = tenantRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _dbContext = dbContext;
            _userManager = userManager;
        }

        public async Task<ServiceResult> Add(AddTenantDto Dto)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(Dto.Subdomain))
                {
                    return new ServiceResult(false, "Subdomain is required");
                }

                if (string.IsNullOrWhiteSpace(Dto.CompanyName))
                {
                    return new ServiceResult(false, "Company name is required");
                }

                // Check if subdomain already exists
                var existingTenant = await _tenantRepository.GetBySubdomainAsync(Dto.Subdomain);
                if (existingTenant != null)
                {
                    return new ServiceResult(false, "Subdomain already exists");
                }

                var request = _mapper.Map<Tenant>(Dto);
                request.IsActive = true;
                await _unitOfWork.Repository<Tenant>().AddAsync(request);
                _unitOfWork.Commit();
                var response = _mapper.Map<GetTenantDto>(request);
                return new ServiceResult(true, response);
            }
            catch (DbUpdateException dbEx)
            {
                return new ServiceResult(false, $"Database error: {dbEx.InnerException?.Message ?? dbEx.Message}");
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, $"Error adding tenant: {ex.Message}");
            }
        }

        public Task<ServiceResult> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> Get(int id)
        {
           var respose= await _unitOfWork.Repository<Tenant>().GetByIdAsync(id);
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
            
            // OPTIMIZATION: Get IQueryable and count BEFORE pagination
            var query =  _tenantRepository.GetActiveWithUser(model.Name,model.userName,model.categoryId);
            
            // Execute count query first (single database call)
            var totalCount = await query.CountAsync();
            
            // Apply pagination at database level and materialize
            var page = await query
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var mappedTenants = page.Select(tenant =>
            {
                var isCustomer = tenant.Customers != null &&
                                 tenant.Customers.Any(c => c.LoyaltyCards != null &&
                                                           c.LoyaltyCards.Any(lc => lc.IsActive));

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

                category.RequireConfirmation = dto.RequireConfirmation ? true : false ;
                if (dto.ConfirmationTtlMinutes != null)
                {
                    category.ConfirmationTtlMinutes = dto.ConfirmationTtlMinutes;
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
                var response = _mapper.Map<GetTenantDto>(category);
                return new ServiceResult(true, response.Id);
            }
            return new ServiceResult(false);
        }
        public async Task<IReadOnlyList<HomeTenantDto>> GetHomeTenantsAsync(CancellationToken ct = default)
        {
            var projections = await _tenantRepository.ListHomeTenantsAsync(ct);

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
