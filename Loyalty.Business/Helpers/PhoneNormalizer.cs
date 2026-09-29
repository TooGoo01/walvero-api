namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Telefon normalizasiyası. Walvero app "+994XXXXXXXXX" göndərir, iiko da E.164 gözləyir.
    /// </summary>
    public static class PhoneNormalizer
    {
        /// <summary>"0501234567" / "994501234567" / "501234567" / "+994 50 123 45 67" → "+994501234567". Boş → null.</summary>
        public static string? ToE164(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return null;
            if (digits.StartsWith("994") && digits.Length == 12) return "+" + digits;
            if (digits.StartsWith("0") && digits.Length == 10) return "+994" + digits[1..];
            if (digits.Length == 9) return "+994" + digits;
            return "+" + digits;
        }

        /// <summary>DB-də rast gəlinə bilən variantlar (exact-match axtarış üçün): "+994…", "994…", "0…".</summary>
        public static IReadOnlyList<string> Variants(string e164)
        {
            var digits = e164.TrimStart('+');
            var list = new List<string> { "+" + digits, digits };
            if (digits.StartsWith("994") && digits.Length == 12)
                list.Add("0" + digits[3..]);
            return list;
        }
    }
}
