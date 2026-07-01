// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.InteropServices;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Configuration.Options;
using Core.DependencyInjection;
using Microsoft.Azure.Cirrus.Extensions.Version.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Version.Processors;

public class GetVersionProcessor : IProcessor
{
    protected OutputStreamDelegate WriteOutput = null!;
    protected CancellationToken CancellationToken { get; private set; }
    protected IEventPublisher Publisher = null!;

    public IProcessor Init(ProcessorContext context)
    {
        WriteOutput = context.OutputHandler.WriteOutput;
        CancellationToken = context.Publisher.Token;
        Publisher = context.Publisher;
        return this;
    }

    public async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);

        await Publisher.Publish(Events.ProcessorBeforeAPICall, "Gathering version information.");
        var versionInfo = GatherVersionInformation();
        await Publisher.Publish(Events.ProcessorAfterAPICall, "Version information gathered.");

        // Write the structured object, consistent with every other extension. The CLI output
        // formatter serializes it to JSON (like `az version`); PowerShell renders a rich object.
        await WriteOutput(versionInfo);

        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
    }

    // Every section is gathered defensively: the version command must NEVER fail, so a fault in
    // any single gatherer degrades that section to a safe fallback instead of throwing. All data
    // sources are cross-platform .NET APIs (reflection + System.Runtime.InteropServices); nothing
    // here is OS-specific.
    private VersionInformation GatherVersionInformation()
    {
        return new VersionInformation
        {
            AzCli = SafeGet(GetAzCliVersion, static () => "unknown"),
            Runtime = SafeGet(GetRuntimeInformation, static () => new RuntimeInfo()),
            Paths = SafeGet(GetConfigurationPaths, static () => new PathInfo())
        };
    }

    // Runs a gatherer and swallows any failure, returning a safe fallback. The broad catch is
    // intentional and required: `az version` is a diagnostic command that must always succeed,
    // even on a misconfigured or partially initialized host.
    private static T SafeGet<T>(Func<T> gather, Func<T> fallbackFactory)
    {
        try
        {
            return gather();
        }
        catch
        {
            return fallbackFactory();
        }
    }

    private string GetAzCliVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? typeof(GetVersionProcessor).Assembly;
        var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? asm.GetName().Version?.ToString()
                      ?? "unknown";

        return StripGitHash(version);
    }

    /// <summary>
    /// Strips git commit hash metadata from version strings.
    /// Example: "1.0.0+9fd0dfb8..." becomes "1.0.0"
    /// </summary>
    private static string StripGitHash(string version)
    {
        var plusIndex = version.IndexOf('+');
        return plusIndex > 0 ? version.Substring(0, plusIndex) : version;
    }

    private RuntimeInfo GetRuntimeInformation()
    {
        return new RuntimeInfo
        {
            DotNet = RuntimeInformation.FrameworkDescription,
            Os = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString()
        };
    }

    private PathInfo GetConfigurationPaths()
    {
        // Install location never depends on config, so resolve it first and independently.
        var info = new PathInfo
        {
            Install = GetInstallLocation(),
            Config = "unknown",
            Extensions = "unknown"
        };

        // Resolve from the configuration system so the reported paths match what the CLI actually
        // uses (config-driven, honoring user overrides). A corrupt/unreadable config.json makes
        // the config service throw; degrade to the placeholders above rather than fail the command.
        try
        {
            var configurationService = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();

            var configFilePath = configurationService.GetConfigFilePath();
            info.Config = Path.GetDirectoryName(configFilePath) ?? configFilePath;

            info.Extensions = configurationService
                .GetConfiguration<AssemblyLoadingOptions>()
                .VersionedInstallationDir;
        }
        catch
        {
            // Configuration unavailable or invalid — keep the safe placeholders.
        }

        return info;
    }

    // Entry-assembly location is empty (not null) under single-file publish; normalize both to
    // "unknown" and guard the rare reflection failure so this can never throw.
    private static string GetInstallLocation()
    {
        try
        {
            var location = Assembly.GetEntryAssembly()?.Location;
            return string.IsNullOrEmpty(location) ? "unknown" : location;
        }
        catch
        {
            return "unknown";
        }
    }
}
