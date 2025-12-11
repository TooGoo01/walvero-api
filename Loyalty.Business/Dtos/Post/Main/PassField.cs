using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class PassField
    {
        public PassField(string key, string value,string? label) { Key = key; Value = value; Label = label; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string? Label { get; set; }
    }
}
