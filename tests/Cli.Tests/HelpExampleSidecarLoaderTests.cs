using Core.Abstractions;

namespace Cli.Tests;

public class HelpExampleSidecarLoaderTests
{
    [Fact]
    public void Load_ShouldReturnSidecarExamples_WhenSidecarsExist()
    {
        using var fixture = new TempHelpSidecarFixture();

        fixture.Write("LoginProcessor.examples.cli.json", """
        {
          "commandId": "login.connect",
          "examples": [
            { "title": "Login with subscription", "command": "azv2 login connect --subscription-id 0000" }
          ]
        }
        """);

        fixture.Write("LoginProcessor.examples.ps.json", """
        {
          "commandId": "login.connect",
          "examples": [
            { "title": "Login with subscription", "command": "Connect-CirrusAccount -SubscriptionId 0000" }
          ]
        }
        """);

        var result = HelpExampleSidecarLoader.Load(fixture.DirectoryPath, "LoginProcessor");

        Assert.Single(result.Cli);
        Assert.Single(result.PowerShell);
        Assert.Equal("azv2 login connect --subscription-id 0000", result.Cli[0].Command);
        Assert.Equal("Connect-CirrusAccount -SubscriptionId 0000", result.PowerShell[0].Command);
    }

    [Fact]
    public void Load_ShouldUseSeedExamples_WhenSidecarsDoNotExist()
    {
        using var fixture = new TempHelpSidecarFixture();

        var seeds = new[]
        {
            "CLI login | azv2 login connect --subscription-id 0000",
            "PS login | Connect-CirrusAccount -SubscriptionId 0000"
        };

        var result = HelpExampleSidecarLoader.Load(fixture.DirectoryPath, "LoginProcessor", seeds);

        Assert.Single(result.Cli);
        Assert.Single(result.PowerShell);
    }

    [Fact]
    public void Load_ShouldPreferSidecarsOverSeedExamples_WhenBothExist()
    {
        using var fixture = new TempHelpSidecarFixture();

        fixture.Write("LoginProcessor.examples.cli.json", """
        {
          "commandId": "login.connect",
          "examples": [
            { "title": "From sidecar", "command": "azv2 login connect --subscription-id from-sidecar" }
          ]
        }
        """);

        var seeds = new[]
        {
            "Seed CLI | azv2 login connect --subscription-id from-seed"
        };

        var result = HelpExampleSidecarLoader.Load(fixture.DirectoryPath, "LoginProcessor", seeds);

        Assert.Single(result.Cli);
        Assert.Equal("azv2 login connect --subscription-id from-sidecar", result.Cli[0].Command);
    }

    [Fact]
    public void Load_ShouldIgnoreMalformedSeedExamples()
    {
        using var fixture = new TempHelpSidecarFixture();

        var seeds = new[]
        {
            "no separator",
            " | azv2 login connect",
            "title only | ",
            "valid | azv2 login connect --subscription-id 0000"
        };

        var result = HelpExampleSidecarLoader.Load(fixture.DirectoryPath, "LoginProcessor", seeds);

        Assert.Single(result.Cli);
        Assert.Empty(result.PowerShell);
    }

    [Fact]
    public void Load_ShouldThrowInvalidOperationException_WhenSidecarJsonIsInvalid()
    {
        using var fixture = new TempHelpSidecarFixture();

        fixture.Write("LoginProcessor.examples.cli.json", "{ invalid json }");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            HelpExampleSidecarLoader.Load(fixture.DirectoryPath, "LoginProcessor"));

        Assert.Contains("LoginProcessor.examples.cli.json", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TempHelpSidecarFixture : IDisposable
    {
        public TempHelpSidecarFixture()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "cirrus-help-sidecar-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Write(string fileName, string content)
        {
            File.WriteAllText(Path.Combine(DirectoryPath, fileName), content);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}
