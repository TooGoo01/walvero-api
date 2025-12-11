using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Product : EntityBase, IEntity
    {
        public string AzTitle { get; set; }
        public string AzDescription { get; set; }
        public string EnTitle { get; set; }
        public string EnDescription { get; set; }
        public string RuTitle { get; set; }
        public string RuDescription { get; set; }
        public int ServiceId { get; set; }
        public Service Service { get; set; }

        public bool IsActive { get; set; }
        public ICollection<ProductFile> ProductFiles { get; set; }
    }
}
