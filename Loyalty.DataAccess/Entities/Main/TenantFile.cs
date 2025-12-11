using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantFile : File
    {
        public int Id { get; set; }
        public Tenant Tenant { get; set; }
    }
}
