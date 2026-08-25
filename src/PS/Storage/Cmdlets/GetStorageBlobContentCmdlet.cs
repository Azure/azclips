// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.Get, "AzStorageBlobContent", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Storage.Models.BlobTransferResult")]
public sealed class GetStorageBlobContentCmdlet : PSCmdlet
{
    private const string NameParameterSet = "Name";
    private const string UriParameterSet = "Uri";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true, ParameterSetName = NameParameterSet)]
    public string AccountName { get; set; } = "";

    [Parameter(Mandatory = true, ParameterSetName = NameParameterSet)]
    [Alias("Container")]
    public string ContainerName { get; set; } = "";

    [Parameter(Mandatory = true, ParameterSetName = NameParameterSet)]
    [Alias("Blob")]
    public string BlobName { get; set; } = "";

    [Parameter]
    [Alias("Destination", "Path")]
    public string FilePath { get; set; } = "";

    [Parameter(Mandatory = true, ParameterSetName = UriParameterSet, ValueFromPipelineByPropertyName = true)]
    [Alias("Uri", "BlobUri")]
    public string? AbsoluteUri { get; set; }

    [Parameter]
    public SwitchParameter Force { get; set; }

    [Parameter(ParameterSetName = NameParameterSet)]
    public DateTimeOffset? SnapshotTime { get; set; }

    [Parameter(ParameterSetName = NameParameterSet)]
    public string? VersionId { get; set; }

    [Parameter]
    public SwitchParameter CheckMd5 { get; set; }

    [Parameter]
    public string? TagCondition { get; set; }

    [Parameter]
    public SwitchParameter AsJob { get; set; }

    [Parameter]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "StorageBlobError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        string source = AbsoluteUri ?? $"{AccountName}/{ContainerName}/{BlobName}";
        string target = string.IsNullOrWhiteSpace(FilePath) ? Directory.GetCurrentDirectory() : FilePath;
        if (!ShouldProcess(target, $"Download {source}"))
        {
            return;
        }

        bool overwrite = Force.IsPresent;
        string destinationPath = Path.GetFullPath(target);
        bool destinationExists = File.Exists(destinationPath);
        if (!overwrite
            && destinationExists
            && !ShouldContinue($"File '{destinationPath}' already exists. Overwrite it?", null))
        {
            return;
        }
        overwrite = overwrite || destinationExists;

        var parameters = new Dictionary<string, object>
        {
            ["AccountName"] = AccountName,
            ["ContainerName"] = ContainerName,
            ["BlobName"] = BlobName,
            ["FilePath"] = FilePath,
            ["Overwrite"] = overwrite,
            ["CheckMd5"] = CheckMd5.IsPresent,
        };
        AddOptional(parameters, nameof(AbsoluteUri), AbsoluteUri);
        AddOptional(parameters, nameof(SnapshotTime), SnapshotTime?.ToString("O"));
        AddOptional(parameters, nameof(VersionId), VersionId);
        AddOptional(parameters, nameof(TagCondition), TagCondition);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters["Profile"] = Profile!;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var invocation = new CommandInvocation(
            "Storage",
            "Get-AzStorageBlobContent",
            parameters,
            false,
            MyInvocation.MyCommand.ModuleName,
            MyInvocation.MyCommand.Version?.ToString());
        telemetryService.TagInvocation(invocation);

        if (AsJob)
        {
            var job = new StorageTransferJob("Get-AzStorageBlobContent", engine, invocation);
            JobRepository.Add(job);
            WriteObject(job);
            return;
        }

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    private static void AddOptional(IDictionary<string, object> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters[name] = value;
        }
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
