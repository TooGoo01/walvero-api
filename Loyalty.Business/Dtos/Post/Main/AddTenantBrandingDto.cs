using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
        public class AddTenantBrandingDto
        {
            public int TenantId { get; set; }
            public IFormFile? LogoFile { get; set; }
            public string? LogoUrl { get; set; }
            public IFormFile? BackgroundImageFile { get; set; }
            public string? BackgroundImageUrl { get; set; }
            public string PrimaryColor { get; set; } = "#000000";
            public string SecondaryColor { get; set; } = "#FFFFFF";
            public string? Description { get; set; }
            public string? Website { get; set; }
        }
}
