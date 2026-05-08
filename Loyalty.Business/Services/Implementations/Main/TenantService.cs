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
using Microsoft.Extensions.Caching.Memory;
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
        // Filter cache TTL — 60s. Discover-də istifadəçilər tez-tez kateqoriya
        // arasında geri-irəli keçir; eyni filter ilə təkrar request DB hit olmadan
        // cache-dən cavab verir. IsCustomer per-user overlay cache-dən kənar
        // qaldığı üçün təhlükəsizdir.
        private static readonly TimeSpan _filterCacheTtl = TimeSpan.FromSeconds(60);

        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ITenantRepository _tenantRepository;
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMemoryCache _cache;

        public TenantService(
            ITenantRepository tenantRepository,
            IMapper mapper,
            IUnitOfWork unitOfWork,
            LoyaltyWalletDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            IMemoryCache cache)
        {
            _tenantRepository = tenantRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _dbContext = dbContext;
            _userManager = userManager;
            _cache = cache;
        }

        private static string _filterCacheKey(
            string? name, int? categoryId, int? countryId, int? cityId,
            int page, int pageSize)
            => $"tenants:filter:{name}|{categoryId}|{countryId}|{cityId}|p{page}|s{pageSize}";

        /// <summary>
        /// Bütün filter-cache-lərini bir anda invalidate etmək üçün ortaq token.
        /// Add/Update/Delete tenant zamanı CTS reset olur, bütün cache entry-ləri
        /// avtomatik olaraq eviction olunur (token-based invalidation).
        /// </summary>
        private static CancellationTokenSource _filterCacheCts = new();

        private void RegisterFilterCacheToken(MemoryCacheEntryOptions options)
        {
            options.AddExpirationToken(
                new Microsoft.Extensions.Primitives.CancellationChangeToken(_filterCacheCts.Token));
        }

        private static void InvalidateAllFilterCache()
        {
            var oldCts = _filterCacheCts;
            _filterCacheCts = new CancellationTokenSource();
            oldCts.Cancel();
            oldCts.Dispose();
        }

        public async Task<ServiceResult> Add(AddTenantDto Dto)
        {
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

            InvalidateAllFilterCache();
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

        public async Task<ServiceResult> GetActiveUser(int currentPage, int pageSize, TenantSearchDto model, int? currentUserId = null)
        {
            if (pageSize > 100)
                pageSize = 100;

            // === STEP 1: Filter kəşrini cache-dən oxu / DB-dən yarad ===
            // Cache yalnız public data saxlayır (IsCustomer = false her zaman),
            // per-user overlay sonra applied olur — cəsl saxlanması üçün təhlükəsizdir.
            var cacheKey = _filterCacheKey(
                model?.Name, model?.categoryId, model?.countryId, model?.cityId,
                currentPage, pageSize);

            (int totalCount, List<TenantDiscoverDto> tenants) cached;

            if (_cache.TryGetValue<(int, List<TenantDiscoverDto>)>(cacheKey, out cached))
            {
                // Cache hit — DB sorğusu olmadı.
            }
            else
            {
                var query = _tenantRepository.GetActiveFiltered(
                    model?.Name,
                    model?.categoryId,
                    model?.countryId,
                    model?.cityId);

                var freshCount = await query.CountAsync();

                var freshTenants = await query
                    .Skip((currentPage - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new TenantDiscoverDto
                    {
                        Id = t.Id,
                        CompanyName = t.CompanyName,
                        IsCustomer = false, // overlay step set edir
                        Category = t.Category == null
                            ? null
                            : new TenantDiscoverCategoryDto { EnName = t.Category.EnName },
                        LoyaltyPrograms = t.LoyaltyPrograms
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
                                        GoogleLogoUrl = lp.CardTemplate.GoogleLogoUrl,
                                        AppLogoUrl = lp.CardTemplate.AppLogoUrl,
                                        AppStripUrl = lp.CardTemplate.AppStripUrl
                                    }
                            })
                            .ToList(),
                        Branches = t.Branches
                            .Where(b => b.IsActive)
                            .OrderByDescending(b => b.IsMainBranch)
                            .ThenBy(b => b.Name)
                            .Select(b => new TenantDiscoverBranchDto
                            {
                                Id = b.Id,
                                Name = b.Name,
                                Address = b.Address,
                                Latitude = b.Latitude,
                                Longitude = b.Longitude,
                                PhotoUrl = b.PhotoUrl,
                                LogoUrl = b.LogoUrl,
                                IsMainBranch = b.IsMainBranch,
                                Phone = b.Phone,
                                LocationUrl = b.LocationUrl,
                                WazeLocationUrl = b.WazeLocationUrl,
                                MenuUrl = b.MenuUrl,
                                InstagramUrl = b.InstagramUrl,
                                FacebookUrl = b.FacebookUrl,
                                WhatsAppUrl = b.WhatsAppUrl,
                                TelegramUrl = b.TelegramUrl,
                                LinkedInUrl = b.LinkedInUrl,
                                TiktokUrl = b.TiktokUrl,
                                BackgroundColor = b.BackgroundColor,
                                ForegroundColor = b.ForegroundColor
                            })
                            .ToList()
                    })
                    .ToListAsync();

                cached = (freshCount, freshTenants);

                var entryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _filterCacheTtl,
                };
                RegisterFilterCacheToken(entryOptions);
                _cache.Set(cacheKey, cached, entryOptions);
            }

            // === STEP 2: Per-user IsCustomer overlay (heç vaxt cache-lənmir) ===
            // Cache obyektlərini mutate etməmək üçün shallow clone.
            var resultTenants = cached.tenants.Select(t => new TenantDiscoverDto
            {
                Id = t.Id,
                CompanyName = t.CompanyName,
                IsCustomer = false,
                Category = t.Category,
                LoyaltyPrograms = t.LoyaltyPrograms,
                Branches = t.Branches,
            }).ToList();

            if (currentUserId.HasValue && resultTenants.Count > 0)
            {
                var tenantIds = resultTenants.Select(t => t.Id).ToList();

                var customerTenantIds = await _dbContext.Set<Customer>()
                    .AsNoTracking()
                    .Where(c => c.applicationUserId == currentUserId.Value
                             && c.IsActive
                             && tenantIds.Contains(c.TenantId)
                             && c.LoyaltyCards.Any(lc => lc.IsActive && lc.Status == CardStatus.Active))
                    .Select(c => c.TenantId)
                    .Distinct()
                    .ToListAsync();

                var customerSet = customerTenantIds.ToHashSet();
                foreach (var tenant in resultTenants)
                {
                    tenant.IsCustomer = customerSet.Contains(tenant.Id);
                }
            }

            var response = new SearchResult<IEnumerable<TenantDiscoverDto>>
            {
                Value = resultTenants,
                DataCount = cached.totalCount,
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
                    category.CompanyName = dto.CompanyName;
                if (dto.Subdomain != null)
                    category.Subdomain = dto.Subdomain;
                if (dto.ContactEmail != null)
                    category.ContactEmail = dto.ContactEmail;
                if (dto.ContactPhone != null)
                    category.ContactPhone = dto.ContactPhone;
                if (dto.LocationUrl != null)
                    category.LocationUrl = dto.LocationUrl;
                if (dto.InstagramUrl != null)
                    category.InstagramUrl = dto.InstagramUrl;
                if (dto.FacebookUrl != null)
                    category.FacebookUrl = dto.FacebookUrl;
                if (dto.WhatsAppUrl != null)
                    category.WhatsAppUrl = dto.WhatsAppUrl;
                if (dto.TelegramUrl != null)
                    category.TeleqramUrl = dto.TelegramUrl;
                if (dto.LinkedInUrl != null)
                    category.LinkedInUrl = dto.LinkedInUrl;
                if (dto.WazeLocationUrl != null)
                    category.WazeLocationUrl = dto.WazeLocationUrl;
                if (dto.MenuUrl != null)
                    category.MenuUrl = dto.MenuUrl;
                category.ShowOnHome = dto.ShowOnHome;
                category.AcceptAnonymus = dto.AcceptAnonymous;
                category.IsHidden = dto.IsHidden;
                category.IsHasMenu = dto.IsHasMenu;
                category.LinkedInUrl = dto.LinkedInUrl;
                category.WazeLocationUrl = dto.WazeLocationUrl;
                if (dto.MenuUrl != null)
                {
                    category.MenuUrl = dto.MenuUrl;
                }
                category.categoryId = dto.categoryId;
                category.RequireConfirmation = dto.RequireConfirmation;
                category.ConfirmationTtlMinutes = dto.ConfirmationTtlMinutes;

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
                if (dto.admins != null && dto.admins.Any())
                {
                    var selectedUser = await _dbContext.Users
                        .Where(d => dto.admins.Contains(d.Id))
                        .ToListAsync();
                    category.Admins = selectedUser;
                }
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

                InvalidateAllFilterCache();
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
