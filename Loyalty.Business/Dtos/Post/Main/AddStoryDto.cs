using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddStoryDto
    {
        public string? AzTitle { get; set; }
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }
        
        // Small Image - file or URL
        public string? SmallImageUrl { get; set; }
        public IFormFile? SmallImageFile { get; set; }
        
        // Image - file or URL
        public string? ImageUrl { get; set; }
        public IFormFile? ImageFile { get; set; }
        
        public int? TenantId { get; set; }
    }
}
