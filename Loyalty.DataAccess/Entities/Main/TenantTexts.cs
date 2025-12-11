using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantTexts : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public string CardTitle { get; set; } = "Loyalty Card";
        public string? CardSubtitle { get; set; }
        public string? TermsUrl { get; set; }
        public string? PrivacyUrl { get; set; }
        public string? SupportEmail { get; set; }
        public string? SupportPhone { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
