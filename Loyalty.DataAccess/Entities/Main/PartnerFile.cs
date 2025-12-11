using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class PartnerFile : File
    {
        public int Id { get; set; }
        public Partner Partner { get; set; }
    }
}
