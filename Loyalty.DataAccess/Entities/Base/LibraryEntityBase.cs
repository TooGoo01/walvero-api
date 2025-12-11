namespace Loyalty.DataAccess.Entities.Base
{
    public class LibraryEntityBase : EntityBase
    {
        public string Value { get; set; }

        public string Description { get; set; }

        public bool InPower { get; set; } = true;

        public DateTime? InvalidationDate { get; set; }
    }
}
