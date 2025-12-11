using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Icon:File
    {
        public int Id { get; set; }
        public string? Name { get; set; }            // opsional
        public string? IconUrl { get; set; }
        public int CardTemplateId { get; set; }
        public CardTemplate CardTemplate { get; set; }
        public int Order { get; set; } = 0;
    }
}
