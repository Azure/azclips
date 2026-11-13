using System.Text.Json;
using Core.Abstractions;
using Core.Agent.Common;
using Core.Agent.Tools.Env;
using Core.Extensions;
using Tests.Common.Stubs;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Profile read failures must not be mistaken for a signed-out account.
/// </summary>
public class EnvironmentProberTests
{
    // Same serializer settings as EnvSnapshotProvider, which feeds the formatter in production.
    private static readonly JsonSerializerOptions s_json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
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

    [Theory]
    [InlineData("{\"error\":\"help_catalog_regeneration_failed\"}")]
    [InlineData("{\"error\":\"help_catalog_unavailable\",\"hint\":\"Run 'az agent tool docs generate' to rebuild the help catalog.\"}")]
    [InlineData("not json")]
    public async Task CatalogUnreadable_GroupsAreUnknown_AndEnvironmentShowsNeitherLine(string catalog)
    {
        var hints = Hints(("vm", "azclips.compute"), ("aks", "azclips.aks"));
        var prober = new EnvironmentProber(new StubProfileService(), flavor: "cli", azclipsVersion: "1",
            readCommandCatalog: _ => Task.FromResult(catalog), extensionHints: hints);

        var snapshot = await prober.ProbeAsync();
        var block = EnvSnapshotFormatter.Format(JsonSerializer.Serialize(snapshot, s_json));

        Assert.Null(snapshot.CommandGroups);
        Assert.Null(snapshot.InstallableGroups);
        Assert.Contains("</environment>", block);
        Assert.DoesNotContain("command-groups", block);
    }

    [Fact]
    public async Task ValidCatalog_AllHintGroupsInstalled_EnvironmentListsGroupsAndInstallableNone()
    {
        const string catalog = """
            { "groups": [ { "verb": "vm", "summary": "" }, { "verb": "group", "summary": "" } ], "commands": [] }
            """;
        var hints = Hints(("vm", "azclips.compute"), ("vm create", "azclips.compute"), ("group", "azclips.resources"));
        var prober = new EnvironmentProber(new StubProfileService(), flavor: "cli", azclipsVersion: "1",
            readCommandCatalog: _ => Task.FromResult(catalog), extensionHints: hints);

        var snapshot = await prober.ProbeAsync();
        var block = EnvSnapshotFormatter.Format(JsonSerializer.Serialize(snapshot, s_json));

        Assert.Contains("command-groups (installed; not runnable by themselves)\n  group, vm\n", block);
        Assert.Contains("extension package))\n  none\n", block);
    }

    [Fact]
    public async Task InstallableGroups_ExcludeInstalledGroups()
    {
        const string catalog = """
            { "groups": [ { "verb": "vm", "summary": "" } ], "commands": [] }
            """;
        var hints = Hints(("vm", "azclips.compute"), ("vm create", "azclips.compute"),
            ("aks", "azclips.aks"), ("aks nodepool add", "azclips.aks"));
        var prober = new EnvironmentProber(new StubProfileService(), flavor: "cli", azclipsVersion: "1",
            readCommandCatalog: _ => Task.FromResult(catalog), extensionHints: hints);

        var snapshot = await prober.ProbeAsync();

        Assert.Equal([new InstallableGroup("aks", "azclips.aks")], snapshot.InstallableGroups);
    }

    [Fact]
    public async Task InstallableGroups_EmptyHintTable_IsEmpty()
    {
        var prober = new EnvironmentProber(new StubProfileService(), flavor: "cli", azclipsVersion: "1",
            readCommandCatalog: _ => Task.FromResult("""{ "groups": [], "commands": [] }"""),
            extensionHints: new ExtensionHintTable());

        var snapshot = await prober.ProbeAsync();

        Assert.NotNull(snapshot.InstallableGroups);
        Assert.Empty(snapshot.InstallableGroups);
    }

    [Fact]
    public async Task CatalogReaderThrows_ExceptionPropagates()
    {
        var prober = new EnvironmentProber(new StubProfileService(), flavor: "cli", azclipsVersion: "1",
            readCommandCatalog: _ => throw new InvalidOperationException("reader bug"),
            extensionHints: new ExtensionHintTable());

        await Assert.ThrowsAsync<InvalidOperationException>(() => prober.ProbeAsync());
    }

    private static ExtensionHintTable Hints(params (string Key, string PackageId)[] entries)
    {
        var table = new ExtensionHintTable();
        foreach (var (key, packageId) in entries)
            table.Leaves[key] = new ExtensionHint { PackageId = packageId };
        return table;
    }

    private sealed class ThrowingProfileService : StubProfileService
    {
        public override Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("auth backend down");
        public override Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("auth backend down");
    }
}
