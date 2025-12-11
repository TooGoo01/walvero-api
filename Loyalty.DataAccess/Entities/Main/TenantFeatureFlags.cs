using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantFeatureFlags : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public bool ShowTier { get; set; } = true;
        public bool ShowQrOnCard { get; set; } = true;
        public bool EnableApplePush { get; set; } = true;
        public bool EnableGooglePatch { get; set; } = true;
        public DateTime UpdatedAt { get; set; }
    }
}
