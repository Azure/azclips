// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Core.Abstractions;
using Core.Agent.Plan;
using Core.Agent.Tools.Help;
using Core.AssemblyLoading;
using Core.Cli;
using Core.Configuration.Options;
using Core.Extensions;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors;
using Microsoft.Extensions.AI;
using Tests.Common.Stubs;
using Xunit;
using Alpha = Azclips.Tests.Fixtures.SharedAlpha.ExtensionIndex;
using Beta = Azclips.Tests.Fixtures.SharedBeta.ExtensionIndex;
using AgentIndex = Microsoft.Azure.Azclips.Extensions.Agent.ExtensionIndex;
using CliHelp = Core.Cli.Help;

namespace Core.Agent.Tests;

public sealed class HelpCatalogProviderTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void ReaderContractsTrackEveryWriterProperty()
    {
        (Type Writer, Type Reader)[] contracts =
        [
            (typeof(CliHelp.HelpManifest), typeof(HelpManifest)),
            (typeof(CliHelp.HelpCommand), typeof(HelpCommand)),
            (typeof(CliHelp.HelpParameter), typeof(HelpParameter)),
            (typeof(CliHelp.HelpExampleSet), typeof(HelpExampleSet)),
            (typeof(CliHelp.HelpExample), typeof(HelpExample)),
            (typeof(CliHelp.HelpIndex), typeof(HelpIndex)),
            (typeof(CliHelp.HelpGroupIndexEntry), typeof(HelpGroupIndexEntry)),
            (typeof(CliHelp.HelpCommandIndexEntry), typeof(HelpCommandIndexEntry)),
            (typeof(CliHelp.HelpCatalogHeader), typeof(HelpCatalogHeader)),
            (typeof(CliHelp.HelpManifestDocument), typeof(HelpManifestDocument)),
            (typeof(CliHelp.HelpIndexDocument), typeof(HelpIndexDocument)),
        ];
        foreach (var (writer, reader) in contracts)
            Assert.Equal(
                writer.GetProperties().Select(p => $"{p.Name}:{p.PropertyType.Name}").Order(StringComparer.Ordinal),
                reader.GetProperties().Select(p => $"{p.Name}:{p.PropertyType.Name}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ColdRead_UsesRuntimeDiscovery_AndWarmReadsDoNotRegenerate()
    {
        using var fixture = new HelpCatalogFixture();
        fixture.Install("Agent", "0.1.0", typeof(AgentIndex).Assembly, bundled: true);
        fixture.Install("Alpha", "1.0.0", typeof(Alpha).Assembly);
        var expected = CliHelp.ExtensionHelpManifestBuilder.BuildIndex(fixture.BaselineManifests());

        AssertJsonEqual(JsonSerializer.Serialize(expected, Json), await fixture.Provider.ListJsonAsync());
        var indexPath = Path.Combine(fixture.CacheDirectory, HelpCatalogProvider.IndexFileName);
        var bytes = await File.ReadAllBytesAsync(indexPath);
        var timestamp = File.GetLastWriteTimeUtc(indexPath);
        AssertJsonEqual(JsonSerializer.Serialize(expected, Json), await fixture.Provider.ListJsonAsync());
        using var help = JsonDocument.Parse(await fixture.Provider.GetJsonAsync("SHAREDVERB ALPHA"));
        Assert.Equal("sharedverb alpha", help.RootElement.GetProperty("cliName").GetString());
        Assert.Equal(1, fixture.GenerationCount);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(indexPath));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(indexPath));
    }

    [Theory]
    [InlineData("package")]
    [InlineData("agent-extension")]
    [InlineData("dev")]
    public async Task FreshArtifacts_AreReadFromPackagedAndDevLayouts(string layout)
    {
        using var fixture = new HelpCatalogFixture();
        fixture.Install("Alpha", "1.0.0", typeof(Alpha).Assembly);
        var directory = layout switch
        {
            "agent-extension" => Path.Combine(fixture.CliDirectory, "Extensions", ExtensionDiscoveryPaths.PackagePrefix + "Agent"),
            "dev" => Path.GetFullPath(Path.Combine(fixture.CliDirectory, "..", "Core.Agent")),
            _ => fixture.CliDirectory,
        };
        fixture.WriteCatalog(directory);

        using var result = JsonDocument.Parse(await fixture.Provider.GetJsonAsync("sharedverb alpha"));
        Assert.Equal("sharedverb alpha", result.RootElement.GetProperty("cliName").GetString());
        Assert.Equal(0, fixture.GenerationCount);
        Assert.False(Directory.Exists(fixture.CacheDirectory));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("invalid-json")]
    [InlineData("null-body")]
    [InlineData("missing-fields")]
    [InlineData("null-element")]
    [InlineData("schema")]
    [InlineData("core-version")]
    [InlineData("fingerprint")]
    public async Task MissingStaleOrCorruptCatalog_RegeneratesRatherThanReturningEmpty(string damage)
    {
        using var fixture = new HelpCatalogFixture();
        fixture.Install("Alpha", "1.0.0", typeof(Alpha).Assembly);
        fixture.WriteCatalog();
        var path = Path.Combine(fixture.CacheDirectory, HelpCatalogProvider.ManifestFileName);
        var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        switch (damage)
        {
            case "missing": File.Delete(path); break;
            case "invalid-json": await File.WriteAllTextAsync(path, "{bad"); break;
            case "null-body": document["manifests"] = null; break;
            case "missing-fields": document["manifests"]![0]!["commands"]![0]!.AsObject().Remove("examples"); break;
            case "null-element": document["manifests"]![0]!["commands"]![0] = null; break;
            case "schema": document["header"]!["schemaVersion"] = -1; break;
            case "core-version": document["header"]!["coreVersion"] = "obsolete"; break;
            case "fingerprint": document["header"]!["extensionFingerprint"] = "obsolete"; break;
        }
        if (damage is not ("missing" or "invalid-json")) await File.WriteAllTextAsync(path, document.ToJsonString());

        using var result = JsonDocument.Parse(await fixture.Provider.GetJsonAsync("sharedverb alpha"));
        Assert.Equal("sharedverb alpha", result.RootElement.GetProperty("cliName").GetString());
        Assert.Equal(1, fixture.GenerationCount);
    }

    [Fact]
    public async Task ExtensionAddRemoveVersionAndConfiguredRoot_InvalidateTheNextRead()
    {
        using var fixture = new HelpCatalogFixture();
        fixture.Install("Agent", "0.1.0", typeof(AgentIndex).Assembly, bundled: true);
        await fixture.Provider.ListJsonAsync();
        fixture.Install("Sample", "1.0.0", typeof(Alpha).Assembly);
        AssertHasCommand(await fixture.Provider.ListJsonAsync(), "sharedverb alpha");
        var version2 = fixture.Install("Sample", "2.0.0", typeof(Beta).Assembly);
        AssertHasCommand(await fixture.Provider.ListJsonAsync(), "sharedverb beta");
        AssertError(await fixture.Provider.GetJsonAsync("sharedverb alpha"), "not_found");
        Directory.Delete(version2, recursive: true);
        AssertHasCommand(await fixture.Provider.ListJsonAsync(), "sharedverb alpha");
        Directory.Delete(Path.Combine(fixture.InstalledRoot, ExtensionDiscoveryPaths.PackagePrefix + "Sample"), recursive: true);
        AssertError(await fixture.Provider.GetJsonAsync("sharedverb alpha"), "not_found");

        fixture.ChangeInstalledRoot(Path.Combine(fixture.Root, "configured"));
        fixture.Install("Sample", "3.0.0", typeof(Beta).Assembly);
        AssertHasCommand(await fixture.Provider.ListJsonAsync(), "sharedverb beta");
        Assert.Equal(6, fixture.GenerationCount);
    }

    [Fact]
    public async Task EmptyInstalledExtensionsRoot_ReadsBundledHelpLikeTheCliLoader()
    {
        using var fixture = new HelpCatalogFixture();
        fixture.Install("Agent", "0.1.0", typeof(AgentIndex).Assembly, bundled: true);
        fixture.Install("Sample", "1.0.0", typeof(Alpha).Assembly);
        await File.WriteAllTextAsync(fixture.Configuration.GetConfigFilePath(), JsonSerializer.Serialize(new
        {
            assemblyLoading = new { installedExtensionsRoot = "", bypassExtensionCache = true },
        }));
        var expected = CliHelp.ExtensionHelpManifestBuilder.BuildIndex(fixture.BaselineManifests());

        AssertJsonEqual(JsonSerializer.Serialize(expected, Json), await fixture.Provider.ListJsonAsync());
        AssertError(await fixture.Provider.GetJsonAsync("sharedverb alpha"), "not_found");
        Assert.Equal(1, fixture.GenerationCount);
    }

    [Fact]
    public async Task RegistryPinChanges_UseTheCurrentLoaderSelection()
    {
        using var fixture = new HelpCatalogFixture();
        fixture.Install("Sample", "1.0.0", typeof(Alpha).Assembly);
        fixture.Install("Sample", "2.0.0", typeof(Beta).Assembly);
        AssertHasCommand(await fixture.Provider.ListJsonAsync(), "sharedverb beta");
        var registry = new ExtensionRegistry();
        registry.Extensions[ExtensionDiscoveryPaths.PackagePrefix + "Sample"] = new ExtensionRegistryEntry
        {
            Version = "1.0.0", Pinned = true,
        };
        new FileExtensionRegistryStore(Path.Combine(fixture.InstalledRoot, HomePaths.RegistryFileName)).Save(registry);

        AssertHasCommand(await fixture.Provider.ListJsonAsync(), "sharedverb alpha");
        AssertError(await fixture.Provider.GetJsonAsync("sharedverb beta"), "not_found");
        Assert.Equal(2, fixture.GenerationCount);
    }

    [Fact]
    public async Task BundledRebuildAndHelpEdits_InvalidateEvenWithoutVersionChange()
    {
        using var fixture = new HelpCatalogFixture();
        var package = fixture.Install("Sample", "1.0.0", typeof(Alpha).Assembly, bundled: true);
        await fixture.Provider.ListJsonAsync();
        File.Copy(typeof(Beta).Assembly.Location, Path.Combine(package, ExtensionDiscoveryPaths.PackagePrefix + "Sample.dll"), overwrite: true);
        AssertHasCommand(await fixture.Provider.ListJsonAsync(), "sharedverb beta");

        var help = Path.Combine(fixture.CliDirectory, "help", "sharedverb");
        Directory.CreateDirectory(help);
        await File.WriteAllTextAsync(Path.Combine(help, "beta.md"), "---\nsummary: changed\n---");
        await fixture.Provider.ListJsonAsync();
        Assert.Equal(3, fixture.GenerationCount);
    }

    [Theory]
    [InlineData("{\"error\":\"shell_out_nonzero_exit\",\"stderr\":\"PRIVATE DATA\"}", "help_catalog_regeneration_failed")]
    [InlineData("{\"error\":\"shell_out_timeout\"}", "help_catalog_regeneration_failed")]
    [InlineData("not json", "help_catalog_regeneration_failed")]
    [InlineData("{\"generated\":true}", "help_catalog_unavailable")]
    public async Task FailedGeneration_NeverReturnsSuccessShapedFallback(string output, string error)
    {
        using var fixture = new HelpCatalogFixture();
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory, _ => Task.FromResult(output));
        var list = await provider.ListJsonAsync();
        AssertError(list, error);
        Assert.DoesNotContain("PRIVATE DATA", list);
        AssertError(await provider.GetJsonAsync("not real"), error);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("access")]
    [InlineData("bad-image")]
    [InlineData("type-load")]
    [InlineData("extension-load")]
    public async Task ExpectedRegenerationFailures_ReturnExplicitErrors_AndPrefetchSkipsThem(string kind)
    {
        using var fixture = new HelpCatalogFixture();
        Exception failure = kind switch
        {
            "io" => new IOException("PRIVATE read failure"),
            "access" => new UnauthorizedAccessException("PRIVATE access failure"),
            "bad-image" => new BadImageFormatException("PRIVATE extension image"),
            "extension-load" => new ExtensionLoadException("PRIVATE extension load"),
            _ => new TypeLoadException("PRIVATE extension type"),
        };
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory,
            _ => Task.FromException<string>(failure));

        var json = await provider.ListJsonAsync();
        AssertError(json, "help_catalog_regeneration_failed");
        Assert.DoesNotContain("PRIVATE", json);
        AssertError(await provider.GetJsonAsync("vm create"), "help_catalog_regeneration_failed");
        Assert.Null(await FailedCommandHelp.BuildContextAsync(new HelpTools(provider), "vm create"));
        Assert.False(Directory.Exists(fixture.CacheDirectory));
    }

    [Fact]
    public void ExtensionLoadException_IsAnInvalidOperationException() =>
        Assert.IsAssignableFrom<InvalidOperationException>(new ExtensionLoadException("load failed"));

    [Theory]
    [InlineData("invalid-operation")]
    [InlineData("null-reference")]
    [InlineData("wrapped-defect")]
    public async Task UnexpectedFaults_PropagateFromDirectReadsAndHostPrefetch(string kind)
    {
        using var fixture = new HelpCatalogFixture();
        Exception failure = kind switch
        {
            "invalid-operation" => new InvalidOperationException("unexpected defect"),
            "null-reference" => new NullReferenceException("unexpected defect"),
            _ => new Exception("unexpected wrapper", new NullReferenceException("unexpected defect")),
        };
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory,
            _ => Task.FromException<string>(failure));

        Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() => provider.ListJsonAsync()));
        Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() => provider.GetJsonAsync("vm create")));
        Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() =>
            FailedCommandHelp.BuildContextAsync(new HelpTools(provider), "vm create")));
        Assert.False(Directory.Exists(fixture.CacheDirectory));
    }

    [Theory]
    [InlineData("json")]
    [InlineData("setting-value")]
    public async Task RealConfigurationFailures_RemainExplicitReadErrors(string damage)
    {
        using var fixture = new HelpCatalogFixture();
        var path = fixture.Configuration.GetConfigFilePath();
        if (damage == "json")
        {
            await File.WriteAllTextAsync(path, "{PRIVATE invalid JSON");
            Assert.Throws<InvalidInputException>(() => fixture.Configuration.GetConfiguration<AssemblyLoadingOptions>());
        }
        else
        {
            var json = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            json["assemblyLoading"]!["bypassExtensionCache"] = "PRIVATE invalid boolean";
            await File.WriteAllTextAsync(path, json.ToJsonString());
            Assert.Throws<InvalidOperationException>(() => fixture.Configuration.GetConfiguration<AssemblyLoadingOptions>());
        }

        var result = await fixture.Provider.ListJsonAsync();
        AssertError(result, "help_catalog_read_failed");
        Assert.DoesNotContain("PRIVATE", result);
        AssertError(await fixture.Provider.GetJsonAsync("vm create"), "help_catalog_read_failed");
        Assert.Null(await FailedCommandHelp.BuildContextAsync(new HelpTools(fixture.Provider), "vm create"));
        Assert.Equal(0, fixture.GenerationCount);
    }

    [Fact]
    public async Task MissingCliVersionFile_RemainsAnExplicitReadError()
    {
        using var fixture = new HelpCatalogFixture();
        File.Delete(Path.Combine(fixture.CliDirectory, "az.dll"));

        AssertError(await fixture.Provider.ListJsonAsync(), "help_catalog_read_failed");
        AssertError(await fixture.Provider.GetJsonAsync("vm create"), "help_catalog_read_failed");
        Assert.Null(await FailedCommandHelp.BuildContextAsync(new HelpTools(fixture.Provider), "vm create"));
        Assert.Equal(0, fixture.GenerationCount);
    }

    [Fact]
    public async Task NullDiscoveryFromRealLoader_IsTheDocumentedLegitimateEmptyCatalog()
    {
        using var fixture = new HelpCatalogFixture();
        var loader = fixture.CreateLoader();
        Assert.Null(loader.LoadAllExtensions());

        using var generated = JsonDocument.Parse(HelpGenerateProcessor.Generate(
            loader, fixture.Configuration, fixture.Executable, fixture.CacheDirectory));
        Assert.True(generated.RootElement.GetProperty("generated").GetBoolean());
        Assert.Equal(0, generated.RootElement.GetProperty("commands").GetInt32());
        AssertJsonEqual("{\"groups\":[],\"commands\":[]}", await fixture.Provider.ListJsonAsync());
        AssertError(await fixture.Provider.GetJsonAsync("vm create"), "not_found");
        Assert.Equal(0, fixture.GenerationCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscoveryFailures_AreNotStampedAsAnEmptyCatalog(bool unexpected)
    {
        using var fixture = new HelpCatalogFixture();
        Exception failure = unexpected
            ? new InvalidOperationException("unexpected discovery defect")
            : new IOException("discovery unavailable");
        var loader = new AssemblyLoadingService(new CLIAssemblyLoadingProvider(
            [new FailingDiscoveryLoader(failure)], new NullLoggingService(), new EmptyExtensionRegistryStore()));
        Assert.Same(failure, Assert.ThrowsAny<Exception>(() => loader.LoadAllExtensions()));
        Assert.Same(failure, Assert.ThrowsAny<Exception>(() => HelpGenerateProcessor.Generate(
            loader, fixture.Configuration, fixture.Executable, fixture.CacheDirectory)));

        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory,
            ct => Task.FromResult(HelpGenerateProcessor.Generate(
                loader, fixture.Configuration, fixture.Executable, fixture.CacheDirectory, ct)));
        if (unexpected)
        {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ListJsonAsync()));
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
                FailedCommandHelp.BuildContextAsync(new HelpTools(provider), "vm create")));
        }
        else
        {
            AssertError(await provider.ListJsonAsync(), "help_catalog_regeneration_failed");
            Assert.Null(await FailedCommandHelp.BuildContextAsync(new HelpTools(provider), "vm create"));
        }
        Assert.False(Directory.Exists(fixture.CacheDirectory));
    }

    [Fact]
    public async Task FailedGenerationCanRecover_AndDoesNotServeAStaleCatalog()
    {
        using var fixture = new HelpCatalogFixture();
        fixture.Install("Sample", "1.0.0", typeof(Alpha).Assembly);
        fixture.WriteCatalog();
        fixture.Install("Sample", "2.0.0", typeof(Beta).Assembly);
        var attempts = 0;
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory, ct =>
            ++attempts == 1 ? Task.FromResult("{\"error\":\"shell_out_spawn_failed\"}") : fixture.RegenerateAsync(ct));
        AssertError(await provider.GetJsonAsync("sharedverb alpha"), "help_catalog_regeneration_failed");
        AssertHasCommand(await provider.ListJsonAsync(), "sharedverb beta");
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task MissingExecutable_UsesTheRealShellErrorPath()
    {
        using var fixture = new HelpCatalogFixture();
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory);
        AssertError(await provider.ListJsonAsync(), "help_catalog_regeneration_failed");
        Assert.False(Directory.Exists(fixture.CacheDirectory));
    }

    [Fact]
    public async Task BrokenExtensionGeneration_IsNotPublishedAsAPartialCatalog()
    {
        using var fixture = new HelpCatalogFixture();
        fixture.Install("Good", "1.0.0", typeof(Alpha).Assembly);
        var broken = fixture.Install("Broken", "1.0.0", typeof(Beta).Assembly);
        await File.WriteAllTextAsync(Path.Combine(broken, ExtensionDiscoveryPaths.PackagePrefix + "Broken.dll"), "not an assembly");
        AssertError(await fixture.Provider.ListJsonAsync(), "help_catalog_regeneration_failed");
        Assert.False(File.Exists(Path.Combine(fixture.CacheDirectory, HelpCatalogProvider.IndexFileName)));
    }

    [Fact]
    public async Task AssemblyWithoutExtensionIndex_IsAnOperationalGenerationFailure()
    {
        using var fixture = new HelpCatalogFixture();
        fixture.Install("NoIndex", "1.0.0", typeof(AgentOptions).Assembly);

        AssertError(await fixture.Provider.ListJsonAsync(), "help_catalog_regeneration_failed");
        Assert.Null(await FailedCommandHelp.BuildContextAsync(new HelpTools(fixture.Provider), "vm create"));
        Assert.False(File.Exists(Path.Combine(fixture.CacheDirectory, HelpCatalogProvider.IndexFileName)));
    }

    [Fact]
    public async Task ExtensionMissingAtLoadTime_IsAnOperationalGenerationFailure()
    {
        using var fixture = new HelpCatalogFixture();
        var provider = new HelpCatalogProvider(
            fixture.Executable, fixture.Configuration, fixture.CacheDirectory, _ =>
            {
                fixture.CreateLoader().LoadExtension("Microsoft.Azure.Azclips.Extensions.Missing");
                return Task.FromResult("{\"generated\":true}");
            });

        AssertError(await provider.ListJsonAsync(), "help_catalog_regeneration_failed");
        Assert.Null(await FailedCommandHelp.BuildContextAsync(new HelpTools(provider), "vm create"));
        Assert.False(File.Exists(Path.Combine(fixture.CacheDirectory, HelpCatalogProvider.IndexFileName)));
    }

    [Fact]
    public async Task ExtensionChangeDuringRegeneration_DoesNotEscapeFreshnessValidation()
    {
        using var fixture = new HelpCatalogFixture();
        fixture.Install("Sample", "1.0.0", typeof(Alpha).Assembly);
        var attempts = 0;
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory, async ct =>
        {
            var result = await fixture.RegenerateAsync(ct);
            if (++attempts == 1) fixture.Install("Sample", "2.0.0", typeof(Beta).Assembly);
            return result;
        });
        AssertError(await provider.ListJsonAsync(), "help_catalog_unavailable");
        AssertHasCommand(await provider.ListJsonAsync(), "sharedverb beta");
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task CancellationDuringGeneration_IsNotConvertedToAReadFailure()
    {
        using var fixture = new HelpCatalogFixture();
        using var cts = new CancellationTokenSource();
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory, _ =>
        {
            cts.Cancel();
            return Task.FromResult("{\"error\":\"shell_out_cancelled\"}");
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.ListJsonAsync(cts.Token));
        Assert.False(Directory.Exists(fixture.CacheDirectory));
    }

    [Fact]
    public async Task HostPrefetch_PreservesCancellationEvenWhenItsOwnTokenIsNotCancelled()
    {
        using var fixture = new HelpCatalogFixture();
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory,
            _ => Task.FromCanceled<string>(new CancellationToken(canceled: true)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            FailedCommandHelp.BuildContextAsync(new HelpTools(provider), "vm create"));
        Assert.False(Directory.Exists(fixture.CacheDirectory));
    }

    [Fact]
    public async Task ConcurrentColdReads_ShareOneGeneration_AndRespectCancellationWhileWaiting()
    {
        using var fixture = new HelpCatalogFixture();
        fixture.Install("Alpha", "1.0.0", typeof(Alpha).Assembly);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new HelpCatalogProvider(fixture.Executable, fixture.Configuration, fixture.CacheDirectory, async ct =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(ct);
            return await fixture.RegenerateAsync(ct);
        });
        var first = provider.ListJsonAsync();
        await started.Task;
        var second = provider.GetJsonAsync("sharedverb alpha");
        using var cts = new CancellationTokenSource();
        var cancelled = provider.ListJsonAsync(cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        release.TrySetResult();
        AssertHasCommand(await first, "sharedverb alpha");
        using var result = JsonDocument.Parse(await second);
        Assert.Equal("sharedverb alpha", result.RootElement.GetProperty("cliName").GetString());
        Assert.Equal(1, fixture.GenerationCount);
    }

    [Fact]
    public async Task CancelledColdAndWarmReads_DoNotGenerateOrReturnHelp()
    {
        using var fixture = new HelpCatalogFixture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Provider.ListJsonAsync(cts.Token));
        fixture.WriteCatalog();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Provider.GetJsonAsync("anything", cts.Token));
        Assert.Equal(0, fixture.GenerationCount);
    }

    [Fact]
    public async Task WriterToProvider_PreservesBaselineJsonAndSingleNameToolContract()
    {
        using var fixture = new HelpCatalogFixture();
        var command = new CliHelp.HelpCommand("vm create", "New-AzVM", "summary", "description",
            [new("name", "Name", "String", true, "name description", ["n"], ["example"], "(All)", true, true, "Identity", "subscription")],
            new([new("CLI", "az vm create --name example")], [new("PS", "New-AzVM -Name example")]),
            [HelpTag.Preview, HelpTag.Deprecated], "preview", "experimental", "deprecated");
        IList<CliHelp.HelpManifest> manifests = [new("vm", "1.0.0", "VMs", [command])];
        fixture.WriteCatalog(manifests: manifests);
        var tools = new HelpTools(fixture.Provider);
        var schema = tools.GetCommand.JsonSchema;
        Assert.Equal("string", schema.GetProperty("properties").GetProperty("name").GetProperty("type").GetString());
        Assert.Equal(new[] { "name" }, schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.False(schema.GetProperty("properties").TryGetProperty("names", out _));
        var expected = JsonSerializer.Serialize(command, Json);
        Assert.Equal(expected, await tools.LookupCommandAsync("NEW-AZVM"));
        Assert.Equal(expected, (await tools.GetCommand.InvokeAsync(new AIFunctionArguments { ["name"] = "vm create" }))?.ToString());
        Assert.Equal(JsonSerializer.Serialize(CliHelp.ExtensionHelpManifestBuilder.BuildIndex(manifests), Json),
            (await tools.ListCommands.InvokeAsync())?.ToString());
        AssertError(await tools.LookupCommandAsync(" "), "missing_name");
        AssertJsonEqual("{\"error\":\"not_found\",\"name\":\"vm\",\"suggestions\":[\"vm create\",\"New-AzVM\"]}",
            await tools.LookupCommandAsync("vm"));
        var context = await FailedCommandHelp.BuildContextAsync(tools, "vm create --name example");
        Assert.Contains(expected, context);
        Assert.Null(await FailedCommandHelp.BuildContextAsync(tools, "unavailable command"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FailedCommandHelp.BuildContextAsync(tools, "vm create", cts.Token));
        Assert.Equal(0, fixture.GenerationCount);
    }

    internal static void AssertError(string json, string code)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(code, document.RootElement.GetProperty("error").GetString());
        Assert.False(document.RootElement.TryGetProperty("commands", out _));
    }

    internal static void AssertJsonEqual(string expected, string actual)
    {
        using var expectedDocument = JsonDocument.Parse(expected);
        using var actualDocument = JsonDocument.Parse(actual);
        Assert.True(JsonElement.DeepEquals(expectedDocument.RootElement, actualDocument.RootElement), actual);
    }

    private static void AssertHasCommand(string json, string name)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Contains(document.RootElement.GetProperty("commands").EnumerateArray(),
            command => command.GetProperty("cliName").GetString() == name);
    }

    private sealed class FailingDiscoveryLoader(Exception failure) : IExtensionLoader
    {
        public Assembly? LoadExtension(string extensionName, string? version = null) => throw new NotSupportedException();
        public IEnumerable<(string Name, string Version, string? Location)> GetInstalledExtensions() => throw failure;
    }
}
