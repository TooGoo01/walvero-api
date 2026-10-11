using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// iiko müştəri kart track-lərinin hesabı (pure). Əsas track inteqrasiyanın <see cref="PosCardTrackMode"/>-una görə
    /// seçilir; tam barkod həmişə (əlavə) track kimi qalır ki, skan işləsin; WalveroId varsa o da əlavə olunur.
    /// </summary>
    public static class PosCardTrack
    {
        public const int MinDigits = 4;
        public const int MaxDigits = 8;
        public const int DefaultDigits = 6;

        /// <summary>4–8 aralığına sıxılmış rəqəm sayı (null → 6).</summary>
        public static int NormalizeDigits(int? digits)
            => Math.Clamp(digits ?? DefaultDigits, MinDigits, MaxDigits);

        /// <summary>Dəyərin son N simvolu; qısadırsa özü.</summary>
        public static string? LastDigits(string? value, int n)
        {
            var v = value?.Trim();
            if (string.IsNullOrEmpty(v)) return null;
            n = Math.Max(1, n);
            return v.Length <= n ? v : v[^n..];
        }

        /// <summary>iiko-ya CardTrack/CardNumber kimi gedən əsas identifikator. Heç biri yoxdursa null.</summary>
        public static string? Primary(PosCardTrackMode mode, int? digits, string? barcode, string? walveroId)
        {
            var bc = Clean(barcode);
            var wid = Clean(walveroId);
            return mode switch
            {
                PosCardTrackMode.WalveroId => wid ?? bc,
                PosCardTrackMode.LastDigits => LastDigits(bc, NormalizeDigits(digits)) ?? wid,
                _ => bc ?? wid
            };
        }

        /// <summary>
        /// iiko müştərisində olmalı bütün track-lər: əsas, tam barkod (həmişə), WalveroId (varsa). Sıra qorunur, təkrar/boş atılır.
        /// </summary>
        public static List<string> AllTracks(PosCardTrackMode mode, int? digits, string? barcode, string? walveroId)
        {
            var list = new List<string>();
            void Add(string? v)
            {
                var c = Clean(v);
                if (c != null && !list.Contains(c, StringComparer.OrdinalIgnoreCase)) list.Add(c);
            }
            Add(Primary(mode, digits, barcode, walveroId));
            Add(barcode);
            Add(walveroId);
            return list;
        }

        /// <summary>Kassirin yazdığı kod qısa koddur? (yalnız rəqəm, uzunluq = N).</summary>
        public static bool IsShortCode(string? code, int? digits)
        {
            var c = Clean(code);
            if (c == null) return false;
            var n = NormalizeDigits(digits);
            return c.Length == n && c.All(char.IsDigit);
        }

        /// <summary>Rejimin oxunaqlı adı (admin UI / log).</summary>
        public static string Describe(PosCardTrackMode mode, int? digits) => mode switch
        {
            PosCardTrackMode.WalveroId => "Walvero ID",
            PosCardTrackMode.LastDigits => $"Son {NormalizeDigits(digits)} rəqəm",
            _ => "Tam barkod"
        };

        private static string? Clean(string? v)
        {
            var t = v?.Trim();
            return string.IsNullOrEmpty(t) ? null : t;
        }
    }
}
