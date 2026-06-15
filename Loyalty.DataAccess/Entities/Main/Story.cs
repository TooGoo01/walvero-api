using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Story:EntityBase,IEntity
    {
        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public string? SmallImageUrl { get; set; }
        public string? ImageUrl { get; set; }
        public string? AzTitle { get; set; }
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }
        public string? VideoUrl { get; set; }
        public string? VideoPosterUrl { get; set; }
        public string? VideoPosterFileName { get; set; }


    }
}
