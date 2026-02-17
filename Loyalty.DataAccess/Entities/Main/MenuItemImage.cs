using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class MenuItemImage:File
    {
        public int MenuItemId { get; set; }
        public MenuItem MenuItem { get; set; }
    }
}
