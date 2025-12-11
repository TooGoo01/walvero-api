using Loyalty.Business.Dtos.Post.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IApplePassSigner
    {
        byte[] Sign(byte[] manifestBytes, string p12Path, string p12Password, string wwdrPath);
        Task SignAsync(SignPassRequest req, CancellationToken ct = default);
    }

}
