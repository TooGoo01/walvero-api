using Microsoft.AspNetCore.Http;
using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Dtos.Post.Main
{
	public class AddAboutDto
	{
        public string AzTitle { get; set; }
        public string AzDescription { get; set; }
        public string EnTitle { get; set; }
        public string EnDescription { get; set; }
        public string RuTitle { get; set; }
        public string RuDescription { get; set; }
        public string? Twitter { get; set; }
        public string? Facebook { get; set; }
        public string? Instagram { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public IFormFileCollection Files { get; set; }
    }
}
