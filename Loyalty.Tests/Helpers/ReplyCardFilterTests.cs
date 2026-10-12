using FluentAssertions;
using Loyalty.Business.Dtos.Chatbot;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

/// <summary>AI cavabının kartları cavab mətnini izləyir.</summary>
public class ReplyCardFilterTests
{
    private static PlaceResultDto C(string name, bool notInApp = false) => new() { Name = name, NotInApp = notInApp };

    private static readonly List<PlaceResultDto> Collected = new()
    {
        C("Khan Musesum Restaurant"), C("Planet"), C("Irish Pub"),
        C("Qala Divari", true), C("Art Garden Restoran", true), C("Shaurma 1 - Səbail", true), C("Shaurma 1 - Nizami", true),
    };

    [Fact]
    public void KeepsOnlyMentioned_InReplyOrder()
    {
        var reply = "Walvero-da İçərişəhərdə məkan yoxdur. Xarici: **Art Garden Restoran** və **Qala Divari** (app-da yoxdur).";
        ReplyCardFilter.Filter(Collected, reply).Select(p => p.Name)
            .Should().Equal("Art Garden Restoran", "Qala Divari");
    }

    [Fact]
    public void DiacriticAndSpacingTolerant_ButBranchSuffixMustMatch()
    {
        var reply = "Yaxınlıqda: Shaurma 1 - Sebail (0,2 km).";
        ReplyCardFilter.Filter(Collected, reply).Select(p => p.Name).Should().Equal("Shaurma 1 - Səbail");
    }

    [Fact]
    public void NoMention_FallsBackToAll()
    {
        ReplyCardFilter.Filter(Collected, "Heç nə tapılmadı.").Should().HaveCount(Collected.Count);
        ReplyCardFilter.Filter(Collected, null).Should().HaveCount(Collected.Count);
    }
}
