// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

internal static class ManagementGroupDeploymentCmdletHelpers
{
    public static object? NormalizeJsonInput(object? value) =>
        value switch
        {
            IDictionary dictionary => dictionary.Cast<DictionaryEntry>().ToDictionary(
                entry => entry.Key.ToString()!,
                entry => NormalizeJsonInput(entry.Value)),
            IEnumerable values when value is not string =>
                values.Cast<object?>().Select(NormalizeJsonInput).ToArray(),
            _ => value,
        };

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

    public static object? RunForOutput(
        PSCmdlet cmdlet,
        IEngine engine,
        OutputStreams streams,
        CancellationToken token,
        string command,
        Dictionary<string, object> parameters)
    {
        object? output = null;
        OutputStreams capturingStreams = streams with
        {
            WriteOutput = (value, _) =>
            {
                output = value is SerializedJson json ? json.Value : value;
                return Task.CompletedTask;
            },
        };
        Run(cmdlet, engine, capturingStreams, token, command, parameters);
        return output;
    }
}
