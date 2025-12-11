using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{

    public sealed class IconDto
    {
        public int? Id { get; set; }                 // mövcuddursa gəlir, yoxsa null
        public string? Name { get; set; }            // opsional
        public string? IconUrl { get; set; }         // mövcud link
        public IFormFile? IconFile { get; set; }     // yeni yükləmə
        public int Order { get; set; } = 0;          // Grid/Horiz/Vert düzülüş üçün
    }
}
