using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Helpers
{
    public static class WalletCodeHelper
    {
       

        public static string Generate6Digit()
        {
            int n = RandomNumberGenerator.GetInt32(0, 1_000_000);
            return n.ToString("D6");
        }
        public static string NewOtp(int len = 6)
        {
            var min = (int)Math.Pow(10, len - 1);
            var max = (int)Math.Pow(10, len) - 1;
            return RandomNumberGenerator.GetInt32(min, max + 1).ToString();
        }

        public static string NewSalt(int bytes = 16)
        {
            var buf = new byte[bytes];
            RandomNumberGenerator.Fill(buf);
            return Convert.ToBase64String(buf);
        }

        public static string HashOtp(string code, string salt)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(salt + code));
            return Convert.ToHexString(bytes);
        }

        public static string MaskPhone(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return "";
            var digits = new string(p.Where(char.IsDigit).ToArray());
            if (digits.Length <= 4) return new string('*', digits.Length);
            return new string('*', digits.Length - 4) + digits[^4..];
        }

    }
}
