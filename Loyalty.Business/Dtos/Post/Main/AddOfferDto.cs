using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddOfferDto
    {
        public string? AzTitle { get; set; }
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }
        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }
        
        // Photo - file or URL
        public string? PhotoUrl { get; set; }
        public IFormFile? PhotoFile { get; set; }

        public int? TenantId { get; set; }

        // Kampaniya metadata (optional)
        public int? DiscountPercent { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public string? AzTerms { get; set; }
        public string? EnTerms { get; set; }
        public string? RuTerms { get; set; }
    }
}
