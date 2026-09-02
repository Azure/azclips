// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.Get, "AzStorageBlob", DefaultParameterSetName = ListParameterSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Storage.Models.StorageBlob")]
public sealed class GetStorageBlobCmdlet : PSCmdlet
{
    private const string ListParameterSet = "List";
    private const string GetParameterSet = "Get";
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string AccountName { get; set; } = "";

    [Parameter(Mandatory = true)]
    [Alias("Container")]
    public string ContainerName { get; set; } = "";

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = GetParameterSet)]
    [Alias("Blob")]
    public string? BlobName { get; set; }

    [Parameter(ParameterSetName = ListParameterSet)]
    public string? Prefix { get; set; }

    [Parameter(ParameterSetName = ListParameterSet)]
    public string? ContinuationToken { get; set; }

    [Parameter(ParameterSetName = ListParameterSet)]
    [ValidateRange(1, int.MaxValue)]
    public int? MaxCount { get; set; }

    [Parameter(ParameterSetName = ListParameterSet)]
    public SwitchParameter IncludeDeleted { get; set; }

    [Parameter(ParameterSetName = ListParameterSet)]
    public SwitchParameter IncludeVersion { get; set; }

    [Parameter]
    public SwitchParameter IncludeTag { get; set; }

    [Parameter]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "StorageBlobError", ErrorCategory.NotSpecified, enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var parameters = new Dictionary<string, object>
        {
            ["AccountName"] = AccountName,
            ["ContainerName"] = ContainerName,
        };
        Add(nameof(BlobName), BlobName);
        Add(nameof(Prefix), Prefix);
        Add("Marker", ContinuationToken);
        if (MaxCount.HasValue)
        {
            parameters["NumResults"] = MaxCount.Value;
        }
        List<string> include = ParameterSetName == ListParameterSet ? ["metadata"] : [];
        if (IncludeDeleted)
        {
            include.Add("deleted");
        }
        if (IncludeVersion)
        {
            include.Add("versions");
        }
        if (IncludeTag)
        {
            if (ParameterSetName == GetParameterSet)
            {
                parameters[nameof(IncludeTag)] = true;
            }
            else
            {
                include.Add("tags");
            }
        }
        if (include.Count > 0)
        {
            parameters["Include"] = include.ToArray();
        }
        Add(nameof(Profile), Profile);

        string commandName = $"Get-AzStorageBlob_{ParameterSetName}";
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var invocation = new CommandInvocation(
            "Storage",
            commandName,
            parameters,
            false,
            MyInvocation.MyCommand.ModuleName,
            MyInvocation.MyCommand.Version?.ToString());
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();

        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parameters[name] = value;
            }
        }
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
