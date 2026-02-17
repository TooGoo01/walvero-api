using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Implementations.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
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

        public MenuItemController(IMenuItemService menuItemService, IMenuItemImageService menuItemImageService)
        {
            _menuItemService = menuItemService;
            _menuItemImageService = menuItemImageService;
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

            var result = await _menuItemService.Add(dto);
            if (result.Success)
            {
               
                    var compressedFiles = CompressImages(dto.Files);

                    // Update model with compressed images
                    var compressedFileCollection = new FormFileCollection(compressedFiles);
                    dto.Files = compressedFileCollection;

                    // Add product and files
                    await _menuItemImageService.AddRangeAsync(dto, (int)result.Data);

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
                Files = dto.Files
            };

            var result = await _menuItemService.Update(updateDto);
            if (result.Success)
            {
                if (dto.Files != null && dto.Files.Count > 0)
                {
                    var compressedFiles = CompressImages(dto.Files);

                    // Update model with compressed images
                    var compressedFileCollection = new FormFileCollection(compressedFiles);
                    dto.Files = compressedFileCollection;

                    // Update product and files
                    await _menuItemImageService.UpdateRangeAsync(dto, id);
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
        [HttpDelete("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _menuItemService.Delete(id);
            if (result.Success)
                return Ok(result);

            return NotFound(result);
        }
    }
}
