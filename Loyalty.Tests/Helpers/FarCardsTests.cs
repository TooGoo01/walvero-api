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
