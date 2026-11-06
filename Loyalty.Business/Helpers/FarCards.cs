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

        /// <summary>
        /// INPBUF içindəki kassa sənədi (docs.rkeeper.ru "XML описание кассового документа").
        /// r_keeper "Pass All Receipts XML data" rejimində <c>&lt;Transactions/&gt;</c> boş gəlir və çekin bütün
        /// məlumatı buradadır: ödənişlər CHECKPAYMENTS, kart balansından ödəniş CHECKDISCOUNTS (PDS endirimi)
        /// və ya Walvero valyutası kimi. Məbləğlər manatla (qəpiklə deyil). Çek yoxdursa null.
        /// </summary>
        public static FarCardsInpBufCheck? ParseInpBufCheck(string xml)
        {
            var doc = Load(xml);
            if (doc == null) return null;

            var inp = doc.Descendants().FirstOrDefault(e => NameIs(e, "INPBUF"));
            if (inp == null) return null;
            var check = inp.Descendants().FirstOrDefault(e => NameIs(e, "CHECK"))
                        // INPBUF mətn/CDATA kimi də gələ bilər
                        ?? Load(inp.Value)?.Descendants().FirstOrDefault(e => NameIs(e, "CHECK"));
            if (check == null) return null;

            int chMode = (int)ParseLong(Attr(check, "chmode"), -1);
            var data = check.Elements().FirstOrDefault(e => NameIs(e, "CHECKDATA"));

            // Bu çağırışın aid olduğu PDS interfeysi: INTERFACES/@current → INTERFACE/@id (eyni kassada bir neçə PDS ola bilər)
            var interfaces = check.Descendants().FirstOrDefault(e => NameIs(e, "INTERFACES"));
            var pdsList = (interfaces?.Elements() ?? Enumerable.Empty<XElement>())
                .Where(e => NameIs(e, "INTERFACE") && string.Equals(Attr(e, "type"), "PDS", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var currentId = interfaces != null ? Attr(interfaces, "current") : null;
            var pds = pdsList.FirstOrDefault(i => !string.IsNullOrEmpty(currentId) && Attr(i, "id") == currentId)
                      ?? (string.IsNullOrEmpty(currentId) ? pdsList.FirstOrDefault() : null);
            string? ifaceCode = pds != null ? Attr(pds, "interface")?.Trim() : null;
            string card = pds?.Elements().Where(e => NameIs(e, "HOLDERS"))
                .SelectMany(h => h.Elements())
                .Select(i => (Attr(i, "cardcode") ?? "").Trim())
                .FirstOrDefault(c => c.Length > 0) ?? "";

            bool IsOurs(XElement e)
            {
                var cc = (Attr(e, "cardcode") ?? "").Trim();
                if (card.Length > 0 && cc.Length > 0 && string.Equals(cc, card, StringComparison.Ordinal)) return true;
                var ic = Attr(e, "interface")?.Trim();
                return !string.IsNullOrEmpty(ifaceCode) && ic == ifaceCode && (cc.Length == 0 || cc == card);
            }

            decimal own = 0m, bonus = 0m;
            if (data != null)
            {
                foreach (var p in data.Elements().Where(e => NameIs(e, "CHECKPAYMENTS")).SelectMany(e => e.Elements()))
                {
                    // bsum — baza valyutada (çek sətirləri ilə eyni vahid); yoxdursa sum
                    decimal amount = Math.Abs(ParseDecimal(Attr(p, "bsum")) ?? ParseDecimal(Attr(p, "sum")) ?? 0m);
                    if (amount == 0m) continue;
                    bool walvero = IsOurs(p) || (Attr(p, "name") ?? "").Contains("walvero", StringComparison.OrdinalIgnoreCase);
                    if (walvero) bonus += amount; else own += amount;
                }
                foreach (var d in data.Elements().Where(e => NameIs(e, "CHECKDISCOUNTS")).SelectMany(e => e.Elements()))
                {
                    // Kart balansından ödəniş kassada "fiskal endirim" kimi qurulub → PDS endirimi (mənfi məbləğ)
                    if (!IsOurs(d)) continue;
                    bonus += Math.Abs(ParseDecimal(Attr(d, "sum")) ?? 0m);
                }
            }

            string restaurant = (Attr(check, "restaurantcode") ?? "").Trim();
            string station = (Attr(check, "stationcode") ?? "").Trim();
            string checkNum = data != null ? (Attr(data, "checknum") ?? "").Trim() : "";
            // checkguid qaytarma sənədində də eynidir (növbə günü dəyişsə belə) → idempotency + ləğv üçün ən etibarlı açar
            string guid = KeyPart(data != null ? Attr(data, "checkguid") : null);
            string day = Digits(Attr(check, "shiftdate"), 8);
            string tail = guid.Length > 0 ? guid
                : checkNum.Length > 0 && checkNum != "0" ? $"{day}:{KeyPart(station)}:{KeyPart(checkNum)}"
                : "";
            string key = tail.Length > 0 ? $"{KeyPart(restaurant)}:{tail}" : "";

            return new FarCardsInpBufCheck(chMode, card, restaurant, key, checkNum, own, bonus);
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

        private static decimal? ParseDecimal(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return decimal.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        private static string KeyPart(string? s) =>
            new string((s ?? "").Trim().Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray());

        private static string Digits(string? s, int max)
        {
            var d = new string((s ?? "").Where(char.IsDigit).ToArray());
            return d.Length > max ? d[..max] : d;
        }

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

    /// <summary>
    /// INPBUF çekinin loyallıq üçün lazım olan hissəsi. Məbləğlər manatla, həmişə ≥ 0 (satış/qaytarma <see cref="ChMode"/> ilə ayrılır).
    /// </summary>
    /// <param name="ChMode">Sənədin növü — <see cref="FarCardsChMode"/>.</param>
    /// <param name="Card">Bu PDS interfeysinə bağlı kart (yoxdursa boş).</param>
    /// <param name="Restaurant">restaurantcode — filial (venue) uyğunluğu üçün.</param>
    /// <param name="CheckKey">Idempotency açarının əsası; çek kimliyi yoxdursa boş.</param>
    /// <param name="CheckNum">Çek nömrəsi (çap olunmayıbsa boş).</param>
    /// <param name="OwnPaid">Qonağın öz pulu (nağd/bank) — qazanma bazası.</param>
    /// <param name="BonusPaid">Kart balansından ödənən (PDS endirimi + Walvero valyutası).</param>
    public sealed record FarCardsInpBufCheck(
        int ChMode, string Card, string Restaurant, string CheckKey, string CheckNum, decimal OwnPaid, decimal BonusPaid);

    /// <summary>CHECK/@chmode dəyərləri ("XML описание кассового документа").</summary>
    public static class FarCardsChMode
    {
        /// <summary>Sifarişin ödənişi (bağlanmış çek).</summary>
        public const int Sale = 1;
        /// <summary>Satışın geri alınması: interfeys təsdiqləyib, kassa ödənişi tamamlaya bilməyib.</summary>
        public const int SaleRollback = 2;
        /// <summary>Çekin qaytarılması / silinməsi.</summary>
        public const int Return = 3;
        /// <summary>Qaytarmanın geri alınması (çek yenidən qüvvədədir).</summary>
        public const int ReturnRollback = 4;
        /// <summary>Tranzaksiyalardan əvvəl yoxlama (protokol 28+).</summary>
        public const int BeforeReceipt = 13;
    }

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
