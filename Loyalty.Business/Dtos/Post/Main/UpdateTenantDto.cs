using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class UpdateTenantDto
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Subdomain { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty; 
        public string? LocationUrl { get; set; } = string.Empty;
        public List<int> admins { get; set; }
        public bool RequireConfirmation { get; init; } = true; // tenant setting
        public int ConfirmationTtlMinutes { get; set; } = 10;
    }
}
