using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetOfferDto
    {
        public int Id { get; set; }
        public string? AzTitle { get; set; }
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }
        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }
        public string? PhotoUrl { get; set; }
        public int? TenantId { get; set; }
        public bool IsActive { get; set; }
        public int? DiscountPercent { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public string? AzTerms { get; set; }
        public string? EnTerms { get; set; }
        public string? RuTerms { get; set; }
    }
}

