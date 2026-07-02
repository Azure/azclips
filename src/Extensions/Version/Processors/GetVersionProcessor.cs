// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.InteropServices;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Configuration.Options;
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
    private VersionInformation GatherVersionInformation()
    {
        var entryAssembly = Assembly.GetEntryAssembly();

        return new VersionInformation
        {
            AzCli = GetAzCliVersion(entryAssembly),
            Runtime = GetRuntimeInformation(),
            Paths = GetConfigurationPaths(entryAssembly)
        };
    }

    private static string GetAzCliVersion(Assembly? entryAssembly)
    {
        if (entryAssembly == null)
        {
            return "unknown";
        }

        return entryAssembly.GetName().Version?.ToString()
                      ?? "unknown";

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

    private static PathInfo GetConfigurationPaths(Assembly? entryAssembly)
    {
        // Install location never depends on config, so resolve it first and independently.
        var info = new PathInfo
        {
            Install = GetInstallLocation(entryAssembly),
            Config = "unknown"
        };

        // Resolve from the configuration system so the reported paths match what the CLI actually
        // uses (config-driven, honoring user overrides).
        var configurationService = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();

        var configFilePath = configurationService.GetConfigFilePath();
        info.Config = Path.GetDirectoryName(configFilePath) ?? configFilePath;

        return info;
    }

    // Entry-assembly location is empty (not null) under single-file publish; normalize to "unknown".
    private static string GetInstallLocation(Assembly? entryAssembly)
    {
        var location = entryAssembly?.Location;
        return string.IsNullOrEmpty(location) ? "unknown" : location;
    }
}
