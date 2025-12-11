using Microsoft.AspNetCore.Http;
using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddBlogDto
    {
        public string AzTitle { get; set; }
        public string AzDescription { get; set; }
        public string EnTitle { get; set; }
        public string EnDescription { get; set; }
        public string RuTitle { get; set; }
        public string RuDescription { get; set; }
        public bool IsActive { get; set; }
        public IFormFileCollection Files { get; set; }
    }
}
