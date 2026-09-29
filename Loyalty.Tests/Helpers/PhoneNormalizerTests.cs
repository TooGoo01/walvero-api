using FluentAssertions;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

public class PhoneNormalizerTests
{
    [Theory]
    [InlineData("994501234567", "+994501234567")]
    [InlineData("+994501234567", "+994501234567")]
    [InlineData("0501234567", "+994501234567")]
    [InlineData("501234567", "+994501234567")]
    [InlineData("+994 50 123-45-67", "+994501234567")]
    [InlineData("79003900938", "+79003900938")]
    public void ToE164_NormalizesKnownShapes(string raw, string expected)
        => PhoneNormalizer.ToE164(raw).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void ToE164_EmptyOrNoDigits_ReturnsNull(string? raw)
        => PhoneNormalizer.ToE164(raw).Should().BeNull();

    [Fact]
    public void Variants_AzNumber_GivesThreeShapes()
        => PhoneNormalizer.Variants("+994501234567").Should()
            .BeEquivalentTo(new[] { "+994501234567", "994501234567", "0501234567" });
}
