using Loyalty.Business.Services.Abstractions.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class OtpGenerator : IOtpGenerator
    {
        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();
        public string GenerateNumeric(int digits)
        {
            Span<byte> b = stackalloc byte[4];
            Rng.GetBytes(b);
            var num = BitConverter.ToUInt32(b) % (uint)Math.Pow(10, digits);
            return num.ToString(new string('0', digits));
        }
    }
}
