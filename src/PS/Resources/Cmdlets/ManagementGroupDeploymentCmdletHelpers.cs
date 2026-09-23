// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

internal static class ManagementGroupDeploymentCmdletHelpers
{
    public static (string ManagementGroupId, string Name) ParseId(string id)
    {
        string[] segments = id.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 8
            || !string.Equals(segments[0], "providers", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[1], "Microsoft.Management", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[2], "managementGroups", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[4], "providers", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[5], "Microsoft.Resources", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[6], "deployments", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Management group deployment Id must match /providers/Microsoft.Management/managementGroups/{managementGroupId}/providers/Microsoft.Resources/deployments/{name}.",
                nameof(id));
        }

        return (segments[3], segments[7]);
    }

    public static void Run(
        PSCmdlet cmdlet,
        IEngine engine,
        OutputStreams streams,
        CancellationToken token,
        string command,
        Dictionary<string, object> parameters)
    {
        ITelemetryService telemetry = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetry.StartRootActivity();
        CommandInvocation invocation = new(
            "Resources",
            command,
            parameters,
            false,
            cmdlet.MyInvocation.MyCommand.ModuleName,
            cmdlet.MyInvocation.MyCommand.Version?.ToString());
        telemetry.TagInvocation(invocation);
        using ISession session = engine.OpenExecutionSession(invocation, streams, token);
        session.Run();
    }
}
