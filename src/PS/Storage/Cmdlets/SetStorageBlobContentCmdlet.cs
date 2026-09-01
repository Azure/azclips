// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Management.Automation;
using System.Collections;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.Set, "AzStorageBlobContent", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Storage.Models.BlobTransferResult")]
public sealed class SetStorageBlobContentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string AccountName { get; set; } = "";

    [Parameter(Mandatory = true)]
    [Alias("Container")]
    public string ContainerName { get; set; } = "";

    [Parameter(Mandatory = true)]
    [Alias("File")]
    public string FilePath { get; set; } = "";

    [Parameter]
    [Alias("Blob")]
    public string BlobName { get; set; } = "";

    [Parameter]
    public SwitchParameter Force { get; set; }

    [Parameter]
    public Hashtable? Metadata { get; set; }

    [Parameter]
    [Alias("Tag")]
    public Hashtable? Tags { get; set; }

    [Parameter]
    public Hashtable? Properties { get; set; }

    [Parameter]
    [ValidateSet("Hot", "Cool", "Cold", "Archive", IgnoreCase = true)]
    public string? StandardBlobTier { get; set; }

    [Parameter]
    public string? EncryptionScope { get; set; }

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
        if (!ShouldProcess($"{AccountName}/{ContainerName}/{BlobName}", "Upload blob content"))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            ["AccountName"] = AccountName,
            ["ContainerName"] = ContainerName,
            ["BlobName"] = BlobName,
            ["FilePath"] = FilePath,
            ["Overwrite"] = Force.IsPresent,
            ["Metadata"] = ToPairs(Metadata),
            ["Tags"] = ToPairs(Tags),
            ["Properties"] = ToPairs(Properties),
        };
        AddOptional(parameters, nameof(StandardBlobTier), StandardBlobTier);
        AddOptional(parameters, nameof(EncryptionScope), EncryptionScope);
        AddOptional(parameters, nameof(TagCondition), TagCondition);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters["Profile"] = Profile!;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var invocation = new CommandInvocation(
            "Storage",
            "Set-AzStorageBlobContent",
            parameters,
            false,
            MyInvocation.MyCommand.ModuleName,
            MyInvocation.MyCommand.Version?.ToString());
        telemetryService.TagInvocation(invocation);

        if (AsJob)
        {
            var job = new StorageTransferJob("Set-AzStorageBlobContent", engine, invocation);
            JobRepository.Add(job);
            WriteObject(job);
            return;
        }

        try
        {
            Run(invocation);
        }
        catch (HttpRequestException exception) when (
            !Force.IsPresent
            && exception.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
        {
            if (!ShouldContinue(
                $"Blob '{AccountName}/{ContainerName}/{BlobName}' already exists. Overwrite it?",
                null))
            {
                return;
            }

            parameters["Overwrite"] = true;
            Run(invocation);
        }

        void Run(CommandInvocation commandInvocation)
        {
            using ISession session = engine.OpenExecutionSession(commandInvocation, streams, _cts.Token);
            session.Run();
        }
    }

    private static string[] ToPairs(Hashtable? values) =>
        values?.Cast<DictionaryEntry>().Select(entry => $"{entry.Key}={entry.Value}").ToArray() ?? [];

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
