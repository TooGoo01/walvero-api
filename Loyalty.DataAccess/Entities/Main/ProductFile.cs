using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class ProductFile:File
    {
        public int Id { get; set; }
        public Product Product { get; set; }
    }
}
