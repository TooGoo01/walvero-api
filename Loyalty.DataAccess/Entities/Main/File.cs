
using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    public class File : EntityBase, IEntity
    {
        public string FileName { get; set; }
        public string Path { get; set; }
    }
}
