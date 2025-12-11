

using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Partner : EntityBase, IEntity
    {
        
        public bool IsActive { get; set; }
        public ICollection<PartnerFile> PartnerFiles { get; set; }
    }
}
