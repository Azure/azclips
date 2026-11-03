// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Agent.Tools.Env;
using Core.Agent.Tools.Help;
using Core.Runtime;
using Core.Runtime.Persistence;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors;
using Tests.Common.Stubs;
using Tests.Common.Utilities;
using Xunit;

namespace Core.Agent.Tests;

public sealed class DirectReadCommandTests
{
    [Fact]
    public async Task GenerateLeafRoutesThroughRealDiscovery_AndHelpCommandsMatchDirectReads()
    {
        using var fixture = new HelpCatalogFixture();
        var home = await CreateChildHomeAsync(fixture);
        var cli = BuiltCliPath();
        using (var cold = JsonDocument.Parse(await RunCliAsync(cli, home, ["agent", "tool", "help", "--name", "agent tool help"], fixture.Root)))
            Assert.Equal("agent tool help", cold.RootElement.GetProperty("cliName").GetString());
        var generated = await RunCliAsync(cli, home, ["agent", "tool", "help", "generate"], fixture.Root);
        using (var document = JsonDocument.Parse(generated))
            Assert.True(document.RootElement.GetProperty("generated").GetBoolean());
        Assert.False(File.Exists(Path.Combine(home, "extensions", "registry.json")));

        var provider = new HelpCatalogProvider(cli, fixture.Configuration, Path.Combine(home, HomePaths.AgentDirectoryName, HomePaths.AgentHelpDirectoryName),
            _ => throw new InvalidOperationException("A warm read must not invoke az."));
        HelpCatalogProviderTests.AssertJsonEqual(
            await RunCliAsync(cli, home, ["agent", "tool", "help"], fixture.Root), await provider.ListJsonAsync());
        HelpCatalogProviderTests.AssertJsonEqual(
            await RunCliAsync(cli, home, ["agent", "tool", "help", "--name", "agent tool help"], fixture.Root),
            await provider.GetJsonAsync("agent tool help"));
        var path = Path.Combine(home, HomePaths.AgentDirectoryName, HomePaths.AgentHelpDirectoryName, HelpCatalogProvider.IndexFileName);
        await File.WriteAllTextAsync(path, "{broken");
        HelpCatalogProviderTests.AssertJsonEqual(
            await RunCliAsync(cli, home, ["agent", "tool", "help"], fixture.Root), await provider.ListJsonAsync());

        var index = new Microsoft.Azure.Azclips.Extensions.Agent.ExtensionIndex();
        Assert.Equal(typeof(HelpGenerateProcessor), index.GetCommandMetadata("agent tool help generate", ClientType.CLI).ProcessorType);
        Assert.Empty(index.GetCommandMetadata("agent tool help", ClientType.CLI).ParameterAliases);
        Assert.Equal(new[] { "name" }, index.GetCommandMetadata("agent tool help", ClientType.CLI).Parameters.Keys);
        Assert.Empty(index.GetCommandMetadata("agent tool help generate", ClientType.CLI).Parameters);
    }

    [Fact]
    public async Task EnvProcessorMatchesDirectProvider_ForSignedInAndSignedOutProfiles()
    {
        using var fixture = new HelpCatalogFixture();
        var home = await CreateChildHomeAsync(fixture);
        var cli = BuiltCliPath();
        var profile = new ProfileService(new NullLoggingService(), new FilePersistenceStore(home));
        foreach (var signedIn in new[] { false, true })
        {
            if (signedIn)
                await profile.SaveAndActivateProfileAsync(new Profile
                {
                    Name = "offline",
                    TenantId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                    SubscriptionId = "11111111-1111-1111-1111-111111111111",
                });

            using var command = JsonDocument.Parse(await RunCliAsync(cli, home, ["agent", "tool", "env"], fixture.Root));
            using var direct = JsonDocument.Parse(await EnvSnapshotProvider.GetJsonAsync(profile, cli));
            foreach (var field in new[] { "os", "shell", "sshSession", "browserAvailable", "signedIn", "subscriptionId", "tenantId", "flavor", "azclipsVersion" })
                Assert.True(JsonElement.DeepEquals(command.RootElement.GetProperty(field), direct.RootElement.GetProperty(field)), field);
            Assert.Equal(fixture.Root, command.RootElement.GetProperty("cwd").GetString());
            Assert.Equal(signedIn, command.RootElement.GetProperty("signedIn").GetBoolean());
            Assert.Equal(direct.RootElement.EnumerateObject().Select(p => p.Name), command.RootElement.EnumerateObject().Select(p => p.Name));
        }
    }

    [Fact]
    public async Task CliHelpReturnsStructuredErrorForAnExtensionWithoutAnIndex()
    {
        using var fixture = new HelpCatalogFixture();
        fixture.Install("NoIndex", "1.0.0", typeof(AgentOptions).Assembly);
        var home = await CreateChildHomeAsync(fixture);

        var output = await RunCliAsync(BuiltCliPath(), home, ["agent", "tool", "help"], fixture.Root);

        HelpCatalogProviderTests.AssertError(output, "help_catalog_regeneration_failed");
        Assert.False(File.Exists(Path.Combine(home, HomePaths.AgentDirectoryName, HomePaths.AgentHelpDirectoryName, HelpCatalogProvider.IndexFileName)));
    }

    [Fact]
    public async Task HelpLeafShowsCatalogRegenerationGuidance()
    {
        using var fixture = new HelpCatalogFixture();
        var home = await CreateChildHomeAsync(fixture);

        var output = await RunCliAsync(BuiltCliPath(), home, ["agent", "tool", "help", "--help"], fixture.Root);

        Assert.Contains("local help catalog", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("az agent tool help generate", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConcurrentGenerationProcesses_PublishReadableCatalogs()
    {
        using var fixture = new HelpCatalogFixture();
        var home = await CreateChildHomeAsync(fixture);
        var cli = BuiltCliPath();
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
            RunCliAsync(cli, home, ["agent", "tool", "help", "generate"], fixture.Root)));
        foreach (var result in results)
        {
            using var generated = JsonDocument.Parse(result);
            Assert.True(generated.RootElement.GetProperty("generated").GetBoolean());
        }

        var provider = new HelpCatalogProvider(cli, fixture.Configuration, Path.Combine(home, HomePaths.AgentDirectoryName, HomePaths.AgentHelpDirectoryName),
            _ => throw new InvalidOperationException("Concurrent publishers must leave a fresh catalog."));
        using var command = JsonDocument.Parse(await provider.GetJsonAsync("agent tool help generate"));
        Assert.Equal("agent tool help generate", command.RootElement.GetProperty("cliName").GetString());
    }

    [Fact]
    public void AgentProcessorCancellation_IsAvailableToReadAndGeneratePaths()
    {
        foreach (var processor in new AgentProcessorBase[] { new HelpGenerateProcessor(), new HelpProcessor(), new EnvProcessor() })
        {
            Assert.False(processor.Token.IsCancellationRequested);
            processor.Cancel();
            Assert.True(processor.Token.IsCancellationRequested);
        }
    }

    private static async Task<string> CreateChildHomeAsync(HelpCatalogFixture fixture)
    {
        var home = Path.Combine(fixture.Root, "cli-home");
        Directory.CreateDirectory(home);
        await File.WriteAllTextAsync(Path.Combine(home, "config.json"), JsonSerializer.Serialize(new
        {
            agent = new { enabled = false },
            telemetry = new { enabled = false },
            assemblyLoading = new { installedExtensionsRoot = fixture.InstalledRoot, bypassExtensionCache = true },
        }));
        return home;
    }

    private static string BuiltCliPath()
    {
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        return Path.Combine(RepoLocator.GetRepoRoot(), "artifacts", configuration, "Core.Cli",
            OperatingSystem.IsWindows() ? "az.exe" : "az");
    }

    private static async Task<string> RunCliAsync(string cli, string home, string[] arguments, string callerDirectory)
    {
        var start = new ProcessStartInfo(cli)
        {
            WorkingDirectory = Path.GetDirectoryName(cli)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment[HomePaths.HomeOverrideVariable] = home;
        start.Environment["AZCLIPS_IN_AGENT_MODE"] = "1";
        start.Environment["AZCLIPS_CALLER_CWD"] = callerDirectory;
        start.Environment["AZCLIPS_LOG_LEVEL"] = "error";
        start.Environment["AZCLIPS_BENCH_RESOLVE_ONLY"] = "false";
        using var process = Process.Start(start)!;
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        Assert.True(process.ExitCode == 0, await errors);
        return await output;
    }
}
