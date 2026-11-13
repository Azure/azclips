using Core.Abstractions;
using Core.Agent.Tools.Env;
using Tests.Common.Stubs;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Profile read failures must not be mistaken for a signed-out account.
/// </summary>
public class EnvironmentProberTests
{
    [Fact]
    public async Task ProfileService_Throws_ProberDoesNotReportSignedOut()
    {
        var prober = new EnvironmentProber(new ThrowingProfileService(), flavor: "azure cli");

        await Assert.ThrowsAsync<InvalidOperationException>(() => prober.ProbeAsync());
    }

    [Fact]
    public async Task CommandGroups_ComeFromCatalogGroups()
    {
        const string catalog = """
            { "groups": [ { "verb": "vm", "summary": "" }, { "verb": "aks", "summary": "" } ], "commands": [] }
            """;
        var prober = new EnvironmentProber(new StubProfileService(), flavor: "cli", azclipsVersion: "1",
            readCommandCatalog: _ => Task.FromResult(catalog));

        var snapshot = await prober.ProbeAsync();

        Assert.Equal(["aks", "vm"], snapshot.CommandGroups);
    }

    [Fact]
    public async Task CommandGroups_CatalogError_IsEmpty()
    {
        var prober = new EnvironmentProber(new StubProfileService(), flavor: "cli", azclipsVersion: "1",
            readCommandCatalog: _ => Task.FromResult("{\"error\":\"help_catalog_regeneration_failed\"}"));

        var snapshot = await prober.ProbeAsync();

        Assert.Empty(snapshot.CommandGroups);
    }

    private sealed class ThrowingProfileService : StubProfileService
    {
        public override Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("auth backend down");
        public override Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("auth backend down");
    }
}
