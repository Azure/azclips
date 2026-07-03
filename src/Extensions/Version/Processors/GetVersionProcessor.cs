// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.InteropServices;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Version.Models;

namespace Microsoft.Azure.Azclips.Extensions.Version.Processors;

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

        // `az version` is a purely local gather (reflection + RuntimeInformation) with no API call,
        // so it publishes only the processor lifecycle events. The Start/End pair is bracketed by a
        // telemetry activity in the engine; there is no request/response stage to surface.
        var versionInfo = GatherVersionInformation();

        // Write the structured object, consistent with every other extension. The CLI output
        // formatter serializes it to JSON (like `az version`); PowerShell renders a rich object.
        await WriteOutput(versionInfo);

        await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
    }

    // Sections are not wrapped defensively: if something goes wrong, exceptions are bubbled up
    // to diagnose initialization or configuration problems cleanly instead of falling back.
    private static VersionInformation GatherVersionInformation()
    {
        var entryAssembly = Assembly.GetEntryAssembly();

        return new VersionInformation
        {
            AzCli = GetAzCliVersion(entryAssembly),
            Runtime = GetRuntimeInformation(),
            Paths = GetConfigurationPaths()
        };
    }

    private static string GetAzCliVersion(Assembly? entryAssembly)
    {
        if (entryAssembly == null)
        {
            return "unknown";
        }

        // Prefer the InformationalVersion (the value declared in the CLI csproj, e.g. "0.1.3") to
        // match how the CLI version is authored; fall back to the numeric AssemblyName.Version
        // (e.g. "0.1.3.0") only when the attribute is absent.
        var informationalVersion =
            entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(informationalVersion))
        {
            // The .NET SDK appends "+<source revision id>" (SemVer build metadata) to the authored
            // value; strip it so we report just the declared version (e.g. "0.1.3" instead of
            // "0.1.3+9f50d509..."). Any pre-release suffix before the '+' (e.g. "-beta") is preserved.
            var buildMetadataStart = informationalVersion.IndexOf('+');
            return buildMetadataStart < 0
                ? informationalVersion
                : informationalVersion[..buildMetadataStart];
        }

        return entryAssembly.GetName().Version?.ToString() ?? "unknown";
    }

    private static RuntimeInfo GetRuntimeInformation()
    {
        return new RuntimeInfo
        {
            DotNet = RuntimeInformation.FrameworkDescription,
            Os = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString()
        };
    }

    private static PathInfo GetConfigurationPaths()
    {
        // Install location never depends on config, so resolve it first and independently.
        var info = new PathInfo
        {
            InstallLocation = GetInstallLocation(),
            ConfigLocation = "unknown"
        };

        // Resolve from the configuration system so the reported paths match what the CLI actually
        // uses (config-driven, honoring user overrides).
        var configurationService = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();

        var configFilePath = configurationService.GetConfigFilePath();
        info.ConfigLocation = Path.GetDirectoryName(configFilePath) ?? configFilePath;

        return info;
    }

    // Report the app base directory — a stable, directory-like path that also works under single-file
    // publish (unlike Assembly.Location, which is empty there). Mirrors how the CLI derives its
    // install/base directory in AssemblyLoadingOptions. The trailing directory separator is trimmed so
    // the value reads like the Config path (e.g. "...\Core.Cli" instead of "...\Core.Cli\").
    private static string GetInstallLocation()
    {
        var baseDirectory = AppContext.BaseDirectory;
        return string.IsNullOrEmpty(baseDirectory)
            ? "unknown"
            : Path.TrimEndingDirectorySeparator(baseDirectory);
    }
}
