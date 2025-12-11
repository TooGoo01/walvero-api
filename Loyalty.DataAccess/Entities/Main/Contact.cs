

using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Contact : EntityBase, IEntity
    {
        public string Subject { get; set; }
        public string Message { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        //public string Phone { get; set; }
    }
}
