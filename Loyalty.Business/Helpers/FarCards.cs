using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// r_keeper FarCards HTTP protokolu (ExtDllHTTP.dll) — XML parse / build (DB-siz, test olunur).
    /// Spec: docs.rkeeper.ru "Описание HTTP протокола для FarCards". POST text/xml, UTF-8.
    /// Məbləğlər qəpikdə (Summa). Root/element adlarının hərf registri sənədlərdə qarışıqdır (ROOT/Root) —
    /// parse registrdən asılı deyil.
    /// </summary>
    public static class FarCardsXml
    {
        public static FarCardsCardQuery? ParseCardQuery(string xml)
        {
            var doc = Load(xml);
            var qry = doc?.Descendants().FirstOrDefault(e => NameIs(e, "QRY"));
            if (qry == null) return null;
            var card = Attr(qry, "Card");
            if (string.IsNullOrWhiteSpace(card)) return null;
            return new FarCardsCardQuery(card.Trim(), Attr(qry, "Restaurant"), Attr(qry, "UnitNo"));
        }

        public static List<FarCardsTransaction>? ParseTransactions(string xml)
        {
            var doc = Load(xml);
            if (doc == null) return null;
            // <Root><Transactions><TransactionsEx .../>...</Transactions><INPBUF>...</INPBUF></Root>
            return doc.Descendants()
                .Where(e => NameIs(e, "TransactionsEx"))
                .Select(e => new FarCardsTransaction(
                    Card: (Attr(e, "Card") ?? "").Trim(),
                    Account: ParseLong(Attr(e, "Account")),
                    PersonId: ParseLong(Attr(e, "PersonID")),
                    Kind: (int)ParseLong(Attr(e, "Kind"), -1),
                    Summa: ParseLong(Attr(e, "Summa")),
                    Restaurant: Attr(e, "Restaurant"),
                    RkDate: Attr(e, "RKDate"),
                    RkUnit: Attr(e, "RKUnit"),
                    RkCheck: Attr(e, "RKCheck")))
                .ToList();
        }

        public static string BuildCardInfo(FarCardsCardInfo info)
        {
            var el = new XElement("GetCardInfoEx",
                new XAttribute("CardCode", info.CardCode),
                new XAttribute("Account", info.Account),
                new XAttribute("PersonID", info.PersonId),
                new XAttribute("Deleted", info.Deleted ? 1 : 0),
                new XAttribute("Seize", 0),
                new XAttribute("Locked", info.Locked ? 1 : 0),
                new XAttribute("WhyLock", Truncate(info.WhyLock, 255)),
                // Endirim/bonusu r_keeper hesablamasın — qazancı Walvero özü edir (Kind=3 potratı üzrə)
                new XAttribute("Discount", 0),
                new XAttribute("Bonus", 0),
                new XAttribute("Summa", info.BalanceMinor),
                // DLL strukturunda Holder 40 baytlıq ASCIIZ-dir
                new XAttribute("Holder", Truncate(info.Holder, 40)),
                new XAttribute("DopInfo", Truncate(info.DopInfo, 255)),
                new XAttribute("ScrMessage", Truncate(info.ScreenMessage, 255)),
                new XAttribute("PrnMessage", Truncate(info.PrintMessage, 255)),
                new XAttribute("Result", 0));
            return Serialize(new XElement("Root", el));
        }

        public static string BuildCardNotFound() =>
            Serialize(new XElement("Root", new XElement("GetCardInfoEx", new XAttribute("Result", 1))));

        public static string BuildTransactionsOk() =>
            Serialize(new XElement("Root",
                new XElement("TransactionsEx", new XAttribute("Result", 0)),
                new XElement("OutBuf", new XAttribute("OutKind", 1),
                    new XElement("TRRESPONSE", new XAttribute("error_code", 0), new XAttribute("err_text", "")))));

        /// <summary>Result=1 — heç bir tranzaksiya keçmədi (FarCards qismən icranı qəbul etmir).</summary>
        public static string BuildTransactionsError(int errorCode, string errText) =>
            Serialize(new XElement("Root",
                new XElement("TransactionsEx", new XAttribute("Result", 1)),
                new XElement("OutBuf", new XAttribute("OutKind", 1),
                    new XElement("TRRESPONSE", new XAttribute("error_code", errorCode), new XAttribute("err_text", Truncate(errText, 255))))));

        /// <summary>Balans (₼) → qəpik (Summa), aşağı yuvarlaqlaşdırılır — kassa mövcud olmayan qəpiyi xərcləməsin.</summary>
        public static long ToMinor(decimal amount) =>
            amount <= 0m ? 0L : (long)Math.Floor(amount * 100m);

        public static decimal FromMinor(long minor) => minor / 100m;

        private static XDocument? Load(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;
            try
            {
                // XDocument.Parse default XmlReaderSettings-də DTD qadağandır (XXE yoxdur)
                return XDocument.Parse(xml);
            }
            catch (System.Xml.XmlException)
            {
                return null;
            }
        }

        private static bool NameIs(XElement e, string name) =>
            string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);

        private static string? Attr(XElement e, string name) =>
            e.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;

        private static long ParseLong(string? s, long fallback = 0) =>
            long.TryParse(s?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        private static string Truncate(string? s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max]);

        private static string Serialize(XElement root)
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            var sb = new StringBuilder();
            sb.Append(doc.Declaration).Append('\n').Append(root.ToString(SaveOptions.DisableFormatting));
            return sb.ToString();
        }
    }

    public sealed record FarCardsCardQuery(string Card, string? Restaurant, string? UnitNo);

    public sealed record FarCardsTransaction(
        string Card, long Account, long PersonId, int Kind, long Summa,
        string? Restaurant, string? RkDate, string? RkUnit, string? RkCheck);

    public sealed record FarCardsCardInfo(
        string CardCode, long Account, long PersonId, string Holder, long BalanceMinor,
        bool Deleted = false, bool Locked = false, string? WhyLock = null,
        string? DopInfo = null, string? ScreenMessage = null, string? PrintMessage = null);

    /// <summary>
    /// TransactionsEx Kind dəyərləri (FarCards spec):
    /// 0 ödəniş (mənfi = kartdan çıxış, müsbət = ödənişin ləğvi),
    /// 1 endirim, 2 bonus (müsbət = hesaba yazılma),
    /// 3 qonağın öz pulu ilə ödədiyi (müsbət = ödədi, mənfi = çekin ləğvi).
    /// </summary>
    public static class FarCardsKind
    {
        public const int Payment = 0;
        public const int Discount = 1;
        public const int Bonus = 2;
        public const int Spend = 3;
    }

    /// <summary>Bir çek (+kart) üzrə FarCards tranzaksiyalarının cəmi.</summary>
    public sealed class FarCardsCheckSummary
    {
        /// <summary>{restaurant}:{yyyyMMdd}:{unit}:{check} — idempotency açarının əsası.</summary>
        public string CheckKey { get; init; } = "";
        public string Card { get; init; } = "";
        public long Account { get; init; }
        public string? Restaurant { get; init; }
        public string? RkCheck { get; init; }
        /// <summary>Xalla ödənilən məbləğ (₼, ≥0) — kartdan çıxılacaq.</summary>
        public decimal BonusPaid { get; init; }
        /// <summary>Qonağın öz pulu ilə ödədiyi (₼, ≥0) — qazanma bazası.</summary>
        public decimal OwnPaid { get; init; }
        /// <summary>Çekin ləğvi / qaytarma (Kind 0 müsbət və ya Kind 3 mənfi).</summary>
        public bool IsReturn { get; init; }
        /// <summary>Walvero-nun emal etmədiyi Kind 1/2 sətirləri (yalnız log).</summary>
        public long IgnoredMinor { get; init; }
        /// <summary>0..3 xaricində Kind ilə sıfırdan fərqli məbləğ — naməlum pul hərəkəti.</summary>
        public bool HasUnknownKind { get; init; }
    }

    public static class FarCardsCheck
    {
        public static List<FarCardsCheckSummary> Summarize(IEnumerable<FarCardsTransaction> transactions)
        {
            return transactions
                .GroupBy(t => (Key: CheckKeyOf(t), Card: t.Card))
                .Select(g =>
                {
                    long pay = g.Where(t => t.Kind == FarCardsKind.Payment).Sum(t => t.Summa);
                    long spend = g.Where(t => t.Kind == FarCardsKind.Spend).Sum(t => t.Summa);
                    long ignored = g.Where(t => t.Kind is FarCardsKind.Discount or FarCardsKind.Bonus).Sum(t => t.Summa);
                    bool unknown = g.Any(t => t.Kind is < 0 or > 3 && t.Summa != 0);

                    bool saleSignal = pay < 0 || spend > 0;
                    bool returnSignal = pay > 0 || spend < 0;
                    bool isReturn = returnSignal && !saleSignal;

                    var first = g.First();
                    return new FarCardsCheckSummary
                    {
                        CheckKey = g.Key.Key,
                        Card = g.Key.Card,
                        Account = g.Select(t => t.Account).FirstOrDefault(a => a > 0),
                        Restaurant = first.Restaurant?.Trim(),
                        RkCheck = first.RkCheck?.Trim(),
                        BonusPaid = isReturn ? FarCardsXml.FromMinor(Math.Max(0, pay)) : FarCardsXml.FromMinor(Math.Max(0, -pay)),
                        OwnPaid = isReturn ? FarCardsXml.FromMinor(Math.Max(0, -spend)) : FarCardsXml.FromMinor(Math.Max(0, spend)),
                        IsReturn = isReturn,
                        IgnoredMinor = ignored,
                        HasUnknownKind = unknown
                    };
                })
                .ToList();
        }

        public static string CheckKeyOf(FarCardsTransaction t)
        {
            // RKDate "2014-08-14T00-00-00" → 20140814 (kassa günü). Format fərqli gəlsə rəqəmlərin ilk 8-i.
            var digits = new string((t.RkDate ?? "").Where(char.IsDigit).ToArray());
            var day = digits.Length >= 8 ? digits[..8] : digits;
            return $"{Clean(t.Restaurant)}:{day}:{Clean(t.RkUnit)}:{Clean(t.RkCheck)}";
        }

        private static string Clean(string? s) =>
            new string((s ?? "").Trim().Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray());
    }
}
