
using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class City : EntityBase, IEntity
    {
       
        // Multi-language support
        public string AzName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public string RuName { get; set; } = string.Empty;
        
        public Country? Country { get; set; }
        public int countryId { get; set; }
    }
}
