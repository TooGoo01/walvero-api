using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Implementations.Main;
using Xunit;
using K = Loyalty.Business.Services.Implementations.Main.AzureOpenAiConfigProvider.Keys;

namespace Loyalty.Tests.Helpers;

public class AzureOpenAiConfigResolveTests
{
    private static readonly Dictionary<string, string> OldConfig = new()
    {
        ["AzureOpenAI:Endpoint"] = "https://old.openai.azure.com/",
        ["AzureOpenAI:ApiKey"] = "old-key",
        ["AzureOpenAI:Deployment"] = "gpt-5-mini",
        ["AzureOpenAI:ApiVersion"] = "",
    };

    private static string? Cfg(string k) => OldConfig.TryGetValue(k, out var v) ? v : null;
    private static string? Dec(string c) => c == "CIPHER" ? "new-key" : null;

    [Fact]
    public void NoDb_UsesConfig_AndBlankIsMissing()
    {
        var r = AzureOpenAiConfigProvider.Resolve(new Dictionary<string, string>(), Cfg, Dec);
        r.Source.Should().Be("config");
        r.Endpoint.Should().Be("https://old.openai.azure.com");
        r.ApiKey.Should().Be("old-key");
        r.Deployment.Should().Be("gpt-5-mini");
        r.ApiVersion.Should().BeNull();
        r.IsConfigured.Should().BeTrue();
    }

    [Fact]
    public void FullDb_OverridesEverything_AndDecryptsKey()
    {
        var db = new Dictionary<string, string>
        {
            [K.Endpoint] = "https://walvero-openai.openai.azure.com/openai/deployments/x?api-version=1",
            [K.ApiKey] = WalletSecretPaths.EncryptedPrefix + "CIPHER",
            [K.Deployment] = "gpt-5.6-sol",
            [K.ApiVersion] = "2025-04-01-preview",
            [K.ReasoningEffort] = "none",
        };
        var r = AzureOpenAiConfigProvider.Resolve(db, Cfg, Dec);
        r.Source.Should().Be("db");
        r.Endpoint.Should().Be("https://walvero-openai.openai.azure.com");
        r.ApiKey.Should().Be("new-key");
        r.Deployment.Should().Be("gpt-5.6-sol");
        r.ApiVersion.Should().Be("2025-04-01-preview");
        r.ReasoningEffort.Should().Be("none");
    }

    [Fact]
    public void DbWithoutKey_FallsBackFully_DeploymentNotMixed()
    {
        var db = new Dictionary<string, string>
        {
            [K.Endpoint] = "https://walvero-openai.openai.azure.com",
            [K.Deployment] = "gpt-5.6-sol",
        };
        var r = AzureOpenAiConfigProvider.Resolve(db, Cfg, Dec);
        r.Source.Should().Be("config");
        r.Endpoint.Should().Be("https://old.openai.azure.com");
        r.Deployment.Should().Be("gpt-5-mini");
    }

    [Fact]
    public void UndecryptableKey_FallsBackToConfig_AndReportsError()
    {
        Exception? seen = null;
        var db = new Dictionary<string, string>
        {
            [K.Endpoint] = "https://walvero-openai.openai.azure.com",
            [K.ApiKey] = WalletSecretPaths.EncryptedPrefix + "BROKEN",
        };
        string? Throw(string _) => throw new InvalidOperationException("bad");
        var r = AzureOpenAiConfigProvider.Resolve(db, Cfg, Throw, ex => seen = ex);
        r.Source.Should().Be("config");
        r.ApiKey.Should().Be("old-key");
        seen.Should().NotBeNull();
    }

    [Fact]
    public void PlaintextDbKey_IsAccepted()
    {
        var db = new Dictionary<string, string>
        {
            [K.Endpoint] = "https://walvero-openai.openai.azure.com",
            [K.ApiKey] = "plain-key",
        };
        var r = AzureOpenAiConfigProvider.Resolve(db, Cfg, Dec);
        r.Source.Should().Be("db");
        r.ApiKey.Should().Be("plain-key");
        r.Deployment.Should().Be("gpt-5-mini"); // DB-də yoxdur → config
    }
}
