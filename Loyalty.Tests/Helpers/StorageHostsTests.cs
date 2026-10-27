using FluentAssertions;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

/// <summary>Storage hesabı köçürməsi (walverostorage → walverostoragepl): cari host connection string-dən, köhnə host alias.</summary>
public class StorageHostsTests
{
    private const string NewCs = "DefaultEndpointsProtocol=https;AccountName=walverostoragepl;AccountKey=abc==;EndpointSuffix=core.windows.net";

    [Theory]
    [InlineData(NewCs, "walverostoragepl.blob.core.windows.net")]
    [InlineData("AccountName=WalveroStorage;AccountKey=x", "walverostorage.blob.core.windows.net")]
    [InlineData("BlobEndpoint=https://custom.example.net/;AccountName=foo;AccountKey=x", "custom.example.net")]
    [InlineData("AccountName=acc;EndpointSuffix=core.chinacloudapi.cn", "acc.blob.core.chinacloudapi.cn")]
    public void ParseAccountHost(string cs, string expected)
        => StorageHosts.ParseAccountHost(cs).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("UseDevelopmentStorage=true")]
    public void ParseAccountHost_Missing_IsNull(string? cs)
        => StorageHosts.ParseAccountHost(cs).Should().BeNull();

    [Fact]
    public void Build_CurrentFirst_LegacyDeduped_DefaultAlwaysIncluded()
    {
        var hosts = StorageHosts.Build("walverostoragepl.blob.core.windows.net", "https://old2.blob.core.windows.net/, WALVEROSTORAGE.blob.core.windows.net");
        hosts.Should().Equal(
            "walverostoragepl.blob.core.windows.net",
            "old2.blob.core.windows.net",
            "walverostorage.blob.core.windows.net");
    }

    [Fact]
    public void Build_SameAsDefault_SingleEntry()
        => StorageHosts.Build(StorageHosts.DefaultHost, null).Should().Equal(StorageHosts.DefaultHost);
}
