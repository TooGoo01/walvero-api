using System.Xml.Linq;
using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Implementations.Main;
using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Tests.Helpers;

/// <summary>r_keeper FarCards HTTP — XML parse/build (spec nümunələri), çek təsnifatı və POS sifarişinə map.</summary>
public class FarCardsTests
{
    private const string CardInfoRequest = """
        <?xml version="1.0" encoding="UTF-8"?>
        <ROOT>
          <QRY Card="994100123" Restaurant="199999999" UnitNo="2">
            <INPBUF>
              <CHECK stationcode="2" restaurantcode="199999999" cashservername="FOCUS_MIDSERV2" generateddatetime="2013-08-21T16:23:56">
                <EXTINFO><INTERFACES><INTERFACE type="PDS" id="1" mode="0"><HOLDERS><ITEM cardcode="994100123"/></HOLDERS></INTERFACE></INTERFACES></EXTINFO>
              </CHECK>
            </INPBUF>
          </QRY>
        </ROOT>
        """;

    private static string TxRequest(params (int Kind, long Summa)[] items)
    {
        var rows = string.Join("\n", items.Select(i =>
            $"""<TransactionsEx Card="994100123" PersonID="202" Account="145" Kind="{i.Kind}" Summa="{i.Summa}" Restaurant="73" RKDate="2014-08-14T00-00-00" RKUnit="201" RKCheck="139" VatSumA="86" VatPrcA="1800"/>"""));
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Root>
              <Transactions>
            {rows}
              </Transactions>
              <INPBUF><CHECK stationcode="6" restaurantcode="73" chmode="0"/></INPBUF>
            </Root>
            """;
    }

    /// <summary>Real kassa sorğusu (UCS showroom, 2026-11-06): Transactions boş, çek INPBUF-da.</summary>
    private static string InpBufRequest(
        int chMode = 1,
        string payments = """<PAYMENT id="{16D72549-14D8-4F31-9E2A-0A833D4F5EED}" code="1" name="AZN" uni="110" paytype="1" bsum="4" sum="4"/>""",
        string discounts = """<DISCOUNT id="{444CBC94-D30B-4149-8AD3-56BC2BDE7B50}" code="98" name="WALVERO" uni="103" interface="14" cardcode="789607890001" account="0" sum="-5"/>""",
        string current = "1008308",
        string checkAttrs = """checknum="101150" checkguid="{1FEA0FDD-362C-4080-BE6C-87B19A132039}" """) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <ROOT>
        	<Transactions/>
        	<INPBUF>
        		<CHECK stationcode="1" restaurantcode="111330001" cashservername="MIDTRT" generateddatetime="2026-11-06T14:52:56" chmode="{chMode}" locale="1049" shiftdate="2026-11-05" shiftnum="310" protocolversion="58">
        			<EXTINFO reservation="">
        				<INTERFACES current="{current}">
        					<INTERFACE type="PDS" id="1008308" mode="1" interface="14">
        						<HOLDERS><ITEM cardcode="789607890001"/></HOLDERS>
        						<ALLCARDS><ITEM cardcode="789607890001"/></ALLCARDS>
        					</INTERFACE>
        				</INTERFACES>
        			</EXTINFO>
        			<CHECKDATA printnum="4" fiscdocnum="4" delprintnum="0" delfiscdocnum="0" tablename="22" ordernum="2070/1" closedatetime="2026-11-06T14:52:56" {checkAttrs}>
        				<CHECKLINES count="1">
        					<LINE code="101" name="Amarettissimo" type="dish" price="9" quantity="1" sum="4">
        						<DISCOUNTS><DISCOUNTPART id="x" disclineuni="103" sum="-5"/></DISCOUNTS>
        						<LINEPAYMENTS><LINEPAYMENT id="y" sum="4"/></LINEPAYMENTS>
        					</LINE>
        				</CHECKLINES>
        				<CHECKPAYMENTS count="1">{payments}</CHECKPAYMENTS>
        				<CHECKDISCOUNTS count="1">{discounts}</CHECKDISCOUNTS>
        				<CURRENCIES/>
        			</CHECKDATA>
        		</CHECK>
        	</INPBUF>
        </ROOT>
        """;

    [Fact]
    public void ParseInpBufCheck_RealSale_CashPlusBalanceDiscount()
    {
        var xml = InpBufRequest();
        FarCardsXml.ParseTransactions(xml).Should().BeEmpty();

        var c = FarCardsXml.ParseInpBufCheck(xml);
        c.Should().NotBeNull();
        c!.ChMode.Should().Be(FarCardsChMode.Sale);
        c.Card.Should().Be("789607890001");
        c.Restaurant.Should().Be("111330001");
        c.CheckNum.Should().Be("101150");
        c.OwnPaid.Should().Be(4m);    // nağd AZN
        c.BonusPaid.Should().Be(5m);  // WALVERO endirimi (kart balansından)
        c.CheckKey.Should().Be("111330001:1FEA0FDD-362C-4080-BE6C-87B19A132039");
    }

    [Fact]
    public void ParseInpBufCheck_WalveroCurrencyPayment_CountsAsBalance()
    {
        // Real çek 101147: Walvero valyutası 8 + WALVERO endirimi 1, nağd yoxdur
        var c = FarCardsXml.ParseInpBufCheck(InpBufRequest(
            payments: """<PAYMENT id="{55FF5A34-2598-4711-99A8-2C30D6B94F63}" code="115" name="Walvero" uni="114" paytype="1" bsum="8" sum="8"/>""",
            discounts: """<DISCOUNT code="98" name="WALVERO" interface="14" cardcode="789607890001" account="0" sum="-1"/>"""));
        c!.OwnPaid.Should().Be(0m);
        c.BonusPaid.Should().Be(9m);
    }

    [Fact]
    public void ParseInpBufCheck_ForeignDiscountsAndMixedPayments()
    {
        // Başqa interfeysin / kartsız endirim balans sayılmır; nağd + bank = qazanma bazası; onluq vergül də oxunur
        var c = FarCardsXml.ParseInpBufCheck(InpBufRequest(
            payments: """<PAYMENT code="1" name="AZN" paytype="1" bsum="2,50" sum="2,50"/><PAYMENT code="5" name="VIZA" paytype="2" sum="10.25"/>""",
            discounts: """<DISCOUNT code="7" name="Happy hour" sum="-3"/><DISCOUNT code="9" name="PRIME HILL" interface="6" cardcode="111" sum="-2"/>"""));
        c!.OwnPaid.Should().Be(12.75m);
        c.BonusPaid.Should().Be(0m);
    }

    [Fact]
    public void ParseInpBufCheck_OtherPdsInterfaceCurrent_NoCard()
    {
        // Çağırış başqa PDS interfeysi üçündürsə bizim kart götürülmür
        var c = FarCardsXml.ParseInpBufCheck(InpBufRequest(current: "999"));
        c!.Card.Should().BeEmpty();
        c.BonusPaid.Should().Be(0m);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(13)]
    public void ParseInpBufCheck_KeepsChModeAndSameKeyForReturn(int chMode)
    {
        var c = FarCardsXml.ParseInpBufCheck(InpBufRequest(chMode: chMode));
        c!.ChMode.Should().Be(chMode);
        // Qaytarma sənədi satışla eyni açarı verməlidir (checkguid) — ləğv ilkin çeki tapsın
        c.CheckKey.Should().Be(FarCardsXml.ParseInpBufCheck(InpBufRequest())!.CheckKey);
    }

    [Fact]
    public void ParseInpBufCheck_NoGuid_FallsBackToShiftStationCheckNum_AndEmptyWhenNoIdentity()
    {
        FarCardsXml.ParseInpBufCheck(InpBufRequest(checkAttrs: """checknum="101150" """))!
            .CheckKey.Should().Be("111330001:20261105:1:101150");
        FarCardsXml.ParseInpBufCheck(InpBufRequest(checkAttrs: """checknum="0" """))!
            .CheckKey.Should().BeEmpty();
    }

    [Fact]
    public void ParseInpBufCheck_NoCheckOrGarbage_ReturnsNull()
    {
        FarCardsXml.ParseInpBufCheck("<Root><Transactions/></Root>").Should().BeNull();
        FarCardsXml.ParseInpBufCheck("<Root><Transactions/><INPBUF/></Root>").Should().BeNull();
        FarCardsXml.ParseInpBufCheck("not xml").Should().BeNull();
    }

    [Fact]
    public void ParseInpBufCheck_EscapedTextInpBuf_IsParsed()
    {
        var inner = """<CHECK chmode="1" restaurantcode="7" stationcode="2" shiftdate="2026-11-05"><EXTINFO><INTERFACES current="1"><INTERFACE type="PDS" id="1" interface="14"><HOLDERS><ITEM cardcode="555"/></HOLDERS></INTERFACE></INTERFACES></EXTINFO><CHECKDATA checknum="9"><CHECKPAYMENTS><PAYMENT name="AZN" sum="3"/></CHECKPAYMENTS></CHECKDATA></CHECK>""";
        var xml = new XElement("Root", new XElement("Transactions"), new XElement("INPBUF", inner)).ToString();
        var c = FarCardsXml.ParseInpBufCheck(xml);
        c!.Card.Should().Be("555");
        c.OwnPaid.Should().Be(3m);
        c.CheckKey.Should().Be("7:20261105:2:9");
    }

    // --- Çek versiyaları (eyni checkguid bir neçə dəfə gəlir) ---
    private const string BaseId = "10:111330001:7BB48476-B488-47E3-A7CD-842E5BCA52E6";
    private static (string, decimal, decimal) Closed(string id, decimal total, decimal redeem) => ($"rkeeper:{id}:OrderClosed", total, redeem);
    private static (string, decimal, decimal) Returned(string id) => ($"rkeeper:{id}:OrderReturned", 0m, 0m);

    [Fact]
    public void ResolveCheckVersion_FirstSale_UsesBaseKey()
    {
        var v = RkeeperLoyaltyService.ResolveCheckVersion(BaseId, false, 4m, 4m, Array.Empty<(string, decimal, decimal)>());
        v.Should().Be(new RkeeperCheckVersion(BaseId, false, null, 0m));
    }

    [Fact]
    public void ResolveCheckVersion_SameAmountsAgain_IsDuplicate()
    {
        var v = RkeeperLoyaltyService.ResolveCheckVersion(BaseId, false, 4m, 4m, new[] { Closed(BaseId, 4m, 4m) });
        v.Duplicate.Should().BeTrue();
        v.ReverseFirstOrderId.Should().BeNull();
    }

    [Fact]
    public void ResolveCheckVersion_ReclosedWithOtherPayment_ReversesOldThenNewVersion()
    {
        // Real hal (çek 101156 → 101157): 4.00 Walvero, fiskal alınmadı → 0.01 nağd + 3.99 Walvero, eyni checkguid, rollback gəlmir
        var v = RkeeperLoyaltyService.ResolveCheckVersion(BaseId, false, 4m, 3.99m, new[] { Closed(BaseId, 4m, 4m) });
        v.Should().Be(new RkeeperCheckVersion(BaseId + ".r1", false, BaseId, 4m));

        // Tam nağdla yenidən bağlanma: balansdan çıxılan 4.00 geri qayıtmalıdır
        var cash = RkeeperLoyaltyService.ResolveCheckVersion(BaseId, false, 4m, 0m, new[] { Closed(BaseId, 4m, 4m) });
        cash.ReverseFirstOrderId.Should().Be(BaseId);
        cash.RefundOnReverse.Should().Be(4m);
    }

    [Fact]
    public void ResolveCheckVersion_SecondReclose_ChainsVersions()
    {
        var markers = new[] { Closed(BaseId, 4m, 4m), Returned(BaseId), Closed(BaseId + ".r1", 4m, 3.99m) };
        RkeeperLoyaltyService.ResolveCheckVersion(BaseId, false, 4m, 3.99m, markers).Duplicate.Should().BeTrue();
        RkeeperLoyaltyService.ResolveCheckVersion(BaseId, false, 4m, 2m, markers)
            .Should().Be(new RkeeperCheckVersion(BaseId + ".r2", false, BaseId + ".r1", 3.99m));
    }

    [Fact]
    public void ResolveCheckVersion_Return_TargetsActiveVersion()
    {
        var none = Array.Empty<(string, decimal, decimal)>();
        RkeeperLoyaltyService.ResolveCheckVersion(BaseId, true, 4m, 4m, none)
            .Should().Be(new RkeeperCheckVersion(BaseId, false, null, 0m)); // satış hələ növbədə ola bilər
        RkeeperLoyaltyService.ResolveCheckVersion(BaseId, true, 4m, 4m, new[] { Closed(BaseId, 4m, 4m) })
            .OrderId.Should().Be(BaseId);
        RkeeperLoyaltyService.ResolveCheckVersion(BaseId, true, 4m, 3.99m,
                new[] { Closed(BaseId, 4m, 4m), Returned(BaseId), Closed(BaseId + ".r1", 4m, 3.99m) })
            .OrderId.Should().Be(BaseId + ".r1");
        RkeeperLoyaltyService.ResolveCheckVersion(BaseId, true, 4m, 4m, new[] { Closed(BaseId, 4m, 4m), Returned(BaseId) })
            .Duplicate.Should().BeTrue(); // artıq qaytarılıb
    }

    [Fact]
    public void ResolveCheckVersion_SaleAfterReturn_OpensNewVersion_AndIgnoresForeignKeys()
    {
        // Qaytarmanın ləğvi (chmode 4) və ya qaytarılmış çekin yenidən satışı
        var markers = new[]
        {
            Closed(BaseId, 4m, 4m), Returned(BaseId),
            Closed(BaseId + "0", 9m, 9m),            // başqa çek (prefiks oxşardır) — nəzərə alınmamalıdır
            ("rkeeper:" + BaseId + ".rX:OrderClosed", 1m, 1m)
        };
        RkeeperLoyaltyService.ResolveCheckVersion(BaseId, false, 4m, 4m, markers)
            .Should().Be(new RkeeperCheckVersion(BaseId + ".r1", false, null, 0m));
    }

    [Fact]
    public void ParseCardQuery_SpecSample()
    {
        var q = FarCardsXml.ParseCardQuery(CardInfoRequest);
        q.Should().NotBeNull();
        q!.Card.Should().Be("994100123");
        q.Restaurant.Should().Be("199999999");
        q.UnitNo.Should().Be("2");
    }

    [Fact]
    public void ParseCardQuery_GarbageOrDtd_ReturnsNull()
    {
        FarCardsXml.ParseCardQuery("not xml").Should().BeNull();
        FarCardsXml.ParseCardQuery("").Should().BeNull();
        // XXE: DTD qadağandır → parse uğursuz → null
        FarCardsXml.ParseCardQuery("""<?xml version="1.0"?><!DOCTYPE r [<!ENTITY x SYSTEM "file:///c:/windows/win.ini">]><ROOT><QRY Card="&x;"/></ROOT>""")
            .Should().BeNull();
    }

    [Fact]
    public void ParseTransactions_ReadsAllFields()
    {
        var txs = FarCardsXml.ParseTransactions(TxRequest((0, -500), (3, 1500)));
        txs.Should().HaveCount(2);
        txs![0].Should().BeEquivalentTo(new FarCardsTransaction("994100123", 145, 202, 0, -500, "73", "2014-08-14T00-00-00", "201", "139"));
        txs[1].Kind.Should().Be(3);
        txs[1].Summa.Should().Be(1500);
    }

    [Fact]
    public void BuildCardInfo_HasResultZeroAndMinorBalance()
    {
        var xml = FarCardsXml.BuildCardInfo(new FarCardsCardInfo("994100123", 77, 5, "Ali Veliyev", FarCardsXml.ToMinor(12.345m)));
        var el = XDocument.Parse(xml).Root!.Element("GetCardInfoEx")!;
        el.Attribute("Result")!.Value.Should().Be("0");
        el.Attribute("Summa")!.Value.Should().Be("1234");
        el.Attribute("Account")!.Value.Should().Be("77");
        el.Attribute("Discount")!.Value.Should().Be("0");
        el.Attribute("Bonus")!.Value.Should().Be("0");
        el.Attribute("Locked")!.Value.Should().Be("0");
    }

    [Fact]
    public void BuildCardInfo_TruncatesHolderTo40()
    {
        var xml = FarCardsXml.BuildCardInfo(new FarCardsCardInfo("1", 1, 1, new string('a', 60), 0));
        XDocument.Parse(xml).Root!.Element("GetCardInfoEx")!.Attribute("Holder")!.Value.Should().HaveLength(40);
    }

    [Fact]
    public void BuildResponses_NotFoundAndErrors()
    {
        XDocument.Parse(FarCardsXml.BuildCardNotFound()).Root!.Element("GetCardInfoEx")!.Attribute("Result")!.Value.Should().Be("1");

        var ok = XDocument.Parse(FarCardsXml.BuildTransactionsOk()).Root!;
        ok.Element("TransactionsEx")!.Attribute("Result")!.Value.Should().Be("0");

        var err = XDocument.Parse(FarCardsXml.BuildTransactionsError(4, "Walvero: balans kifayet etmir")).Root!;
        err.Element("TransactionsEx")!.Attribute("Result")!.Value.Should().Be("1");
        err.Descendants("TRRESPONSE").Single().Attribute("error_code")!.Value.Should().Be("4");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(0.009, 0)]
    [InlineData(12.999, 1299)]
    public void ToMinor_FloorsAndClampsNegative(decimal amount, long expected) =>
        FarCardsXml.ToMinor(amount).Should().Be(expected);

    [Fact]
    public void Summarize_Sale_BonusPaymentAndOwnSpend()
    {
        var s = FarCardsCheck.Summarize(FarCardsXml.ParseTransactions(TxRequest((0, -500), (3, 1500)))!).Single();
        s.IsReturn.Should().BeFalse();
        s.BonusPaid.Should().Be(5m);
        s.OwnPaid.Should().Be(15m);
        s.Account.Should().Be(145);
        s.CheckKey.Should().Be("73:20140814:201:139");
        s.HasUnknownKind.Should().BeFalse();
    }

    [Fact]
    public void Summarize_Return_PaymentCancelAndNegativeSpend()
    {
        var s = FarCardsCheck.Summarize(FarCardsXml.ParseTransactions(TxRequest((0, 500), (3, -1500)))!).Single();
        s.IsReturn.Should().BeTrue();
        s.BonusPaid.Should().Be(5m);
        s.OwnPaid.Should().Be(15m);
    }

    [Fact]
    public void Summarize_DiscountAndBonusKinds_IgnoredNotUnknown()
    {
        var s = FarCardsCheck.Summarize(FarCardsXml.ParseTransactions(TxRequest((1, -200), (2, 300), (3, 1000)))!).Single();
        s.IgnoredMinor.Should().Be(100);
        s.HasUnknownKind.Should().BeFalse();
        s.OwnPaid.Should().Be(10m);
        s.BonusPaid.Should().Be(0m);
    }

    [Fact]
    public void Summarize_UnknownKindWithAmount_Flagged()
    {
        FarCardsCheck.Summarize(FarCardsXml.ParseTransactions(TxRequest((160, 15)))!).Single()
            .HasUnknownKind.Should().BeTrue();
        FarCardsCheck.Summarize(FarCardsXml.ParseTransactions(TxRequest((160, 0)))!).Single()
            .HasUnknownKind.Should().BeFalse();
    }

    [Fact]
    public void CheckKey_SameCheckDifferentTime_SameKey()
    {
        var a = new FarCardsTransaction("1", 0, 0, 3, 1, "73", "2014-08-14T00-00-00", "201", "139");
        var b = a with { RkDate = "2014-08-14T18-30-00" };
        FarCardsCheck.CheckKeyOf(a).Should().Be(FarCardsCheck.CheckKeyOf(b));
    }

    [Fact]
    public void MapToOrderEvent_Sale_BonusAsCredentialPayment()
    {
        var check = FarCardsCheck.Summarize(FarCardsXml.ParseTransactions(TxRequest((0, -500), (3, 1500)))!).Single();
        var card = new LoyaltyCard { Id = 145, CardNumber = "LC-1" };

        var req = RkeeperLoyaltyService.MapToOrderEvent(check, card, "9:" + check.CheckKey);

        req.SourcePrefix.Should().Be("rkeeper");
        req.EventType.Should().Be("OrderClosed");
        req.OrderId.Should().Be("9:73:20140814:201:139");
        req.OrderNumber.Should().Be(139);
        req.TotalSum.Should().Be(20m);
        req.CustomerCardNumber.Should().Be("LC-1");
        req.Payments.Should().ContainSingle(p => p.Sum == 5m && p.Credential == "994100123");
    }

    [Theory]
    [InlineData("https://appapi.walvero.com/api/rkeeper/AbC123secret/transactionsex", "https://appapi.walvero.com/api/rkeeper/***/transactionsex")]
    [InlineData("POST /api/rkeeper/AbC123secret/getcardinfoex", "POST /api/rkeeper/***/getcardinfoex")]
    [InlineData("POST /api/wallet/iiko_transaction", "POST /api/wallet/iiko_transaction")]
    public void TelemetryMask_HidesToken(string input, string expected) =>
        Loyalty.WebAPI.Extensions.RkeeperTokenTelemetryInitializer.Mask(input).Should().Be(expected);

    [Fact]
    public void MapToOrderEvent_Return_NoPayments()
    {
        var check = FarCardsCheck.Summarize(FarCardsXml.ParseTransactions(TxRequest((0, 500), (3, -1500)))!).Single();
        var req = RkeeperLoyaltyService.MapToOrderEvent(check, new LoyaltyCard { CardNumber = "LC-1" }, "9:x");
        req.EventType.Should().Be("OrderReturned");
        req.Payments.Should().BeEmpty();
    }
}
