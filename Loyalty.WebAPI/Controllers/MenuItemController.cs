using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Implementations.Main;
using Loyalty.DataAccess.Context;
using Loyalty.WebAPI.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class MenuItemController : ControllerBase
    {
        private readonly IMenuItemService _menuItemService;
        private readonly IMenuItemImageService _menuItemImageService;
        private readonly ITenantProvider _tenantProvider;
        private readonly LoyaltyWalletDbContext _db;

        public MenuItemController(
            IMenuItemService menuItemService,
            IMenuItemImageService menuItemImageService,
            ITenantProvider tenantProvider,
            LoyaltyWalletDbContext db)
        {
            _menuItemService = menuItemService;
            _menuItemImageService = menuItemImageService;
            _tenantProvider = tenantProvider;
            _db = db;
        }

        private bool IsBranchScoped()
            => !User.IsInRole("Tenant Admin")
               && !User.IsInRole("Super Admin")
               && (User.IsInRole("Branch Admin")
                   || User.IsInRole("Branch Moderator")
                   || User.IsInRole("Moderator")
                   // Menu Admin: branch_id claim-i varsa filiala kilidli, yoxdursa tenant-wide
                   || (User.IsInRole("Menu Admin") && _tenantProvider.CurrentBranchId.HasValue));

        /// <summary>
        /// Multi-filial yaratmada item-in kateqoriyasını hədəf filial üçün həll edir:
        /// Ümumi (BranchId=null) və ya öz filialı → olduğu kimi; əks halda hədəf filialda
        /// eyni AzName-li aktiv kateqoriya axtarılır, tapılmasa kopyası yaradılır.
        /// </summary>
        private async Task<int> ResolveCategoryForBranchAsync(
            Loyalty.DataAccess.Entities.Main.MenuCategory source, int branchId)
        {
            if (source.BranchId == null || source.BranchId == branchId)
                return source.Id;

            var existing = await _db.MenuCategories
                .AsNoTracking()
                .Where(mc => mc.TenantId == source.TenantId
                          && mc.BranchId == branchId
                          && mc.IsActive
                          && mc.AzName == source.AzName)
                .Select(mc => (int?)mc.Id)
                .FirstOrDefaultAsync();
            if (existing != null)
                return existing.Value;

            var copy = new Loyalty.DataAccess.Entities.Main.MenuCategory
            {
                AzName = source.AzName,
                EnName = source.EnName,
                RuName = source.RuName,
                AzDescription = source.AzDescription,
                EnDescription = source.EnDescription,
                RuDescription = source.RuDescription,
                DescriptionColor = source.DescriptionColor,
                DescriptionFontSize = source.DescriptionFontSize,
                SortOrder = source.SortOrder,
                TenantId = source.TenantId,
                BranchId = branchId,
                IsActive = true,
                RegDate = DateTime.UtcNow
            };
            _db.MenuCategories.Add(copy);
            await _db.SaveChangesAsync();
            return copy.Id;
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult<List<GetMenuItemDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> List([FromQuery] int? tenantId = null, [FromQuery] int? branchId = null, [FromQuery] int? categoryId = null)
        {
            var resolvedTenantId = tenantId ?? _tenantProvider.CurrentTenantId;
            if (!resolvedTenantId.HasValue)
                return BadRequest(new ServiceResult(false, "Tenant Id təyin olunmayıb"));

            var branchScoped = IsBranchScoped();
            var effectiveBranchId = branchScoped ? _tenantProvider.CurrentBranchId : branchId;

            var result = await _menuItemService.GetByTenantAndBranchAsync(resolvedTenantId.Value, effectiveBranchId, branchScoped, categoryId);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetMenuItemDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _menuItemService.GetAll();
            if (result.Success)
                return Ok(result);
            
            return BadRequest(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetMenuItemDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetAllWithDetails()
        {
            var result = await _menuItemService.GetAllWithDetails();
            if (result.Success)
                return Ok(result);
            
            return BadRequest(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetMenuItemDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetActive()
        {
            var result = await _menuItemService.GetActive();
            if (result.Success)
                return Ok(result);
            
            return BadRequest(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ServiceResult<GetMenuItemDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _menuItemService.GetById(id);
            if (result.Success)
                return Ok(result);
            
            return NotFound(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ServiceResult<GetMenuItemDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetByIdWithDetails(int id)
        {
            var result = await _menuItemService.GetByIdWithDetails(id);
            if (result.Success)
                return Ok(result);
            
            return NotFound(result);
        }

        [HttpGet("{categoryId}")]
        [ProducesResponseType(typeof(ServiceResult<List<GetMenuItemDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetByCategoryId(int categoryId)
        {
            var result = await _menuItemService.GetByCategoryId(categoryId);
            if (result.Success)
                return Ok(result);
            
            return BadRequest(result);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult<GetMenuItemDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Add([FromForm] AddMenuItemDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (IsBranchScoped())
            {
                var ownBranchId = _tenantProvider.CurrentBranchId;
                if (!ownBranchId.HasValue)
                    return StatusCode((int)HttpStatusCode.Forbidden, new ServiceResult(false, "Branch təyin olunmayıb"));
                dto.BranchId = ownBranchId;
                dto.BranchIds = null; // branch-scoped user multi-filial yarada bilməz
            }

            // Şəkilləri bir dəfə sıx — multi-filial dövründə təkrar sıxılmasın
            if (dto.Files != null && dto.Files.Count > 0)
            {
                var compressedFiles = CompressImages(dto.Files);
                dto.Files = new FormFileCollection(compressedFiles);
            }

            // Multi-filial: seçilmiş filialların hamısında ayrıca MenuItem yaradılır
            if (dto.BranchIds is { Count: > 0 })
            {
                var branchIds = dto.BranchIds.Where(b => b > 0).Distinct().ToList();
                if (branchIds.Count == 0)
                    return BadRequest(new ServiceResult(false, "Filial seçilməyib"));

                // Seçilmiş kateqoriyanı tam yüklə — filial üzrə həll (tap/yarat) üçün lazımdır
                var sourceCategory = await _db.MenuCategories.AsNoTracking()
                    .FirstOrDefaultAsync(mc => mc.Id == dto.MenuCategoryId);
                if (sourceCategory == null)
                    return BadRequest(new ServiceResult(false, "Menyu kateqoriyası tapılmadı"));

                var validBranchIds = await _db.Branches.AsNoTracking()
                    .Where(b => branchIds.Contains(b.Id) && b.TenantId == sourceCategory.TenantId && b.IsActive)
                    .Select(b => b.Id)
                    .ToListAsync();
                if (validBranchIds.Count == 0)
                    return BadRequest(new ServiceResult(false, "Seçilmiş filiallar tapılmadı"));

                var createdIds = new List<int>();
                foreach (var branchId in validBranchIds)
                {
                    // Filial-spesifik kateqoriya seçilibsə: hədəf filialda eyni adlı
                    // kateqoriya tapılır, yoxdursa kopyası yaradılır.
                    dto.MenuCategoryId = await ResolveCategoryForBranchAsync(sourceCategory, branchId);
                    dto.BranchId = branchId;
                    var r = await _menuItemService.Add(dto);
                    if (!r.Success) continue;

                    var itemId = (int)r.Data;
                    createdIds.Add(itemId);

                    if (dto.Files != null && dto.Files.Count > 0)
                        await _menuItemImageService.AddRangeAsync(dto, itemId);

                    if (dto.IngredientVideoFile != null)
                        await _menuItemImageService.UploadIngredientVideoAsync(
                            itemId, dto.IngredientVideoFile, remove: false);
                }

                if (createdIds.Count == 0)
                    return BadRequest(new ServiceResult(false, "Menyu elementi yaradıla bilmədi"));

                return Ok(new ServiceResult(true,
                    new { createdCount = createdIds.Count, ids = createdIds },
                    $"{createdIds.Count} filialda yaradıldı"));
            }

            var result = await _menuItemService.Add(dto);
            if (result.Success)
            {
                    // Şəkil yalnız fayl gələndə əlavə olunur (Files null olduqda 500 olmasın).
                    if (dto.Files != null && dto.Files.Count > 0)
                    {
                        await _menuItemImageService.AddRangeAsync(dto, (int)result.Data);
                    }

                    if (dto.IngredientVideoFile != null || dto.RemoveIngredientVideo)
                    {
                        await _menuItemImageService.UploadIngredientVideoAsync(
                            (int)result.Data,
                            dto.IngredientVideoFile,
                            dto.RemoveIngredientVideo);
                    }

                    return Ok(result);

            }


            return BadRequest(result);
        }

        [HttpPut("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult<GetMenuItemDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Update(int id, [FromForm] AddMenuItemDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (IsBranchScoped())
            {
                var ownBranchId = _tenantProvider.CurrentBranchId;
                var existing = await _db.MenuItems.AsNoTracking()
                    .Where(mi => mi.Id == id)
                    .Select(mi => new { mi.BranchId })
                    .FirstOrDefaultAsync();
                if (existing == null) return NotFound(new ServiceResult(false, "Menyu elementi tapılmadı"));
                if (existing.BranchId != ownBranchId)
                    return StatusCode((int)HttpStatusCode.Forbidden, new ServiceResult(false, "Bu menyu elementini dəyişməyə icazəniz yoxdur"));
                dto.BranchId = ownBranchId;
            }

            // Convert AddMenuItemDto to UpdateMenuItemDto
            var updateDto = new UpdateMenuItemDto
            {
                Id = id,
                Currency = dto.Currency,
                AzName = dto.AzName,
                EnName = dto.EnName,
                RuName = dto.RuName,
                AzDescription = dto.AzDescription,
                RuDescription = dto.RuDescription,
                EnDescription = dto.EnDescription,
                PrepTimeMinutes = dto.PrepTimeMinutes,
                CurrencySign = dto.CurrencySign,
                Price = dto.Price,
                DiscountPrice = dto.DiscountPrice,
                MenuCategoryId = dto.MenuCategoryId,
                BranchId = dto.BranchId,
                Files = dto.Files
            };

            var result = await _menuItemService.Update(updateDto);
            if (result.Success)
            {
                // Seçilmiş mövcud şəkilləri sil
                if (dto.RemovedImageIds != null && dto.RemovedImageIds.Count > 0)
                {
                    await _menuItemImageService.DeleteImagesAsync(id, dto.RemovedImageIds);
                }

                // Yeni şəkilləri ƏLAVƏ et (köhnələri silmir)
                if (dto.Files != null && dto.Files.Count > 0)
                {
                    var compressedFiles = CompressImages(dto.Files);
                    dto.Files = new FormFileCollection(compressedFiles);
                    await _menuItemImageService.AddRangeAsync(dto, id);
                }

                if (dto.IngredientVideoFile != null || dto.RemoveIngredientVideo)
                {
                    await _menuItemImageService.UploadIngredientVideoAsync(
                        id,
                        dto.IngredientVideoFile,
                        dto.RemoveIngredientVideo);
                }

                return Ok(result);
            }

            return NotFound(result);
        }
        private List<IFormFile> CompressImages(IFormFileCollection files)
        {
            var compressedFiles = new List<IFormFile>();

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    using (var image = Image.Load(file.OpenReadStream()))
                    {
                        // Resize the image to a max of 800x800
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Mode = ResizeMode.Max,
                            Size = new Size(800, 800)
                        }));

                        // Compress the image with JPEG encoder
                        MemoryStream stream = new MemoryStream();
                        using (var ms = new MemoryStream())
                        {
                            image.Save(ms, new JpegEncoder
                            {
                                Quality = 75 // Adjust quality as needed
                            });

                            ms.Position = 0; // Reset the position of the stream
                            ms.CopyTo(stream);
                            // Create a new FormFile with the compressed image
                            var compressedFile = new FormFile(stream, 0, ms.Length, file.Name, file.FileName)
                            {
                                Headers = file.Headers,
                                ContentType = file.ContentType
                            };

                            compressedFiles.Add(compressedFile);
                        }
                    }
                }
            }

            return compressedFiles;
        }
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Reorder([FromBody] ReorderMenuItemDto dto)
        {
            if (dto.TenantId <= 0)
                dto.TenantId = _tenantProvider.CurrentTenantId ?? 0;
            if (dto.TenantId <= 0)
                return BadRequest(new ServiceResult(false, "Tenant Id təyin olunmayıb"));

            if (IsBranchScoped())
            {
                var own = _tenantProvider.CurrentBranchId;
                if (!own.HasValue)
                    return StatusCode((int)HttpStatusCode.Forbidden, new ServiceResult(false, "Branch təyin olunmayıb"));
                dto.BranchId = own;
            }

            var result = await _menuItemService.ReorderAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Delete(int id)
        {
            if (IsBranchScoped())
            {
                var ownBranchId = _tenantProvider.CurrentBranchId;
                var existing = await _db.MenuItems.AsNoTracking()
                    .Where(mi => mi.Id == id)
                    .Select(mi => new { mi.BranchId })
                    .FirstOrDefaultAsync();
                if (existing == null) return NotFound(new ServiceResult(false, "Menyu elementi tapılmadı"));
                if (existing.BranchId != ownBranchId)
                    return StatusCode((int)HttpStatusCode.Forbidden, new ServiceResult(false, "Bu menyu elementini silməyə icazəniz yoxdur"));
            }

            var result = await _menuItemService.Delete(id);
            if (result.Success)
                return Ok(result);

            return NotFound(result);
        }
    }
}
