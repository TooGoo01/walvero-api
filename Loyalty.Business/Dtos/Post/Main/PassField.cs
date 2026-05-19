using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class PassField
    {
        public PassField(string key, string value, string? label) { Key = key; Value = value; Label = label; }
        public PassField(string key, string value, string? label, string? changeMessage)
        {
            Key = key; Value = value; Label = label; ChangeMessage = changeMessage;
        }
        public string Key { get; set; }
        public string Value { get; set; }
        public string? Label { get; set; }

        // PassKit changeMessage template ("%@" placeholder = new value). When set on a
        // changed field, iOS shows a notification on lock screen / wallet refetch.
        public string? ChangeMessage { get; set; }
    }
}
