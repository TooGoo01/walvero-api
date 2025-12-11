using Loyalty.Business.Dtos.Post.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IApplePassBuilder
    {
        
        Task BuildAsync(BuildPassRequest req, string workingDir, CancellationToken ct = default);
    }
}
