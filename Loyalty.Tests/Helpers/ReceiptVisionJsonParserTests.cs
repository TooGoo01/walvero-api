using FluentAssertions;
using Loyalty.Business.Services.Implementations.Main;
using Xunit;

namespace Loyalty.Tests.Helpers;

public class ReceiptVisionJsonParserTests
{
    [Fact]
    public void Parses_FencedJson_WithAllFields()
    {
        var content = "```json\n{\"amount\": 45.5, \"taxNumber\": \"1701332401\", \"objectCode\": \"OBJ-12\", " +
                      "\"receiptId\": \"ABC123456\", \"dateIso\": \"2026-11-04\", \"orderNo\": \"264\", \"isFiscal\": true, " +
                      "\"merchantName\": \"Papa Cinema\", \"rawText\": \"Papa Cinema\\nYEKUN MEBLEG 45.50\"}\n```";

        var r = ReceiptVisionJsonParser.Parse(content);

        r.Should().NotBeNull();
        r!.Amount.Should().Be(45.5m);
        r.TaxNumber.Should().Be("1701332401");
        r.ObjectCode.Should().Be("OBJ-12");
        r.ReceiptId.Should().Be("ABC123456");
        r.OrderNo.Should().Be("264");
        r.IsFiscal.Should().BeTrue();
        r.MerchantName.Should().Be("Papa Cinema");
        r.RawText.Should().Contain("YEKUN MEBLEG 45.50");
        r.Date.Should().Be(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc));
        r.RawJson.Should().StartWith("{").And.EndWith("}");
        r.Ok.Should().BeFalse(); // Ok-u çağıran təyin edir
    }

    [Fact]
    public void Parses_StringAmount_WithCommaDecimal_AndNumericOrderNo()
    {
        var r = ReceiptVisionJsonParser.Parse("{\"amount\":\"12,50\",\"orderNo\":264,\"isFiscal\":\"true\"}");

        r.Should().NotBeNull();
        r!.Amount.Should().Be(12.50m);
        r.OrderNo.Should().Be("264");
        r.IsFiscal.Should().BeTrue();
    }

    [Fact]
    public void NullFields_StayNull()
    {
        var r = ReceiptVisionJsonParser.Parse(
            "{\"amount\":null,\"taxNumber\":null,\"objectCode\":null,\"receiptId\":null,\"dateIso\":null,\"orderNo\":null,\"isFiscal\":false,\"merchantName\":null,\"rawText\":null}");

        r.Should().NotBeNull();
        r!.Amount.Should().BeNull();
        r.TaxNumber.Should().BeNull();
        r.Date.Should().BeNull();
        r.MerchantName.Should().BeNull();
        r.RawText.Should().BeNull();
        r.IsFiscal.Should().BeFalse();
    }

    [Theory]
    [InlineData("2026-11-04T18:30:00", 2026, 11, 4)]
    [InlineData("2026-11-04", 2026, 11, 4)]
    public void Date_IsNoonUtc_OnTheGivenDay(string iso, int y, int m, int d)
    {
        var r = ReceiptVisionJsonParser.Parse($"{{\"dateIso\":\"{iso}\"}}");
        r!.Date.Should().Be(new DateTime(y, m, d, 12, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no json here")]
    [InlineData("{ broken json")]
    public void Garbage_ReturnsNull_WithoutThrowing(string content)
    {
        var act = () => ReceiptVisionJsonParser.Parse(content);
        act.Should().NotThrow();
        act().Should().BeNull();
    }
}
