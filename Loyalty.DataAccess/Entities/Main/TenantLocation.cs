
using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantLocation : EntityBase, IEntity
    {
        public Tenant? Tenant { get; set; }
        public int? tenantId { get; set; }
        public Country? Country { get; set; }
        public int? countryId { get; set; }
        public City? City { get; set; }
        public int? cityId { get; set; }
        public TimeZone? TimeZone { get; set; }
        public int? timeZoneId { get; set; }
    }
}
