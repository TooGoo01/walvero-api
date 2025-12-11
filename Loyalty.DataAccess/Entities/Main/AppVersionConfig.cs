using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;


namespace Loyalty.DataAccess.Entities.Main
{
    public class AppVersionConfig:EntityBase,IEntity
    {
        public string Platform { get; set; } = default!;

        public bool IsActive { get; set; }       
        public bool IsMandatory { get; set; }    

        public string LatestVersion { get; set; } = default!;
        public string? Description { get; set; }    
        public string? MessageTitle { get; set; }    
        public string? Message { get; set; }    
        public string? StoreUrl { get; set; }

    }
}
