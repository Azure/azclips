// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsStorage;

using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Security;
using Core.Abstractions;
using Core.DependencyInjection;
using CmdletsStorage::Microsoft.Azure.Azclips.Cmdlets.Storage;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public sealed class BlobContentCmdletTests : IDisposable
{
    private readonly string directory = Path.Combine(Environment.CurrentDirectory, "blob-cmdlet-" + Guid.NewGuid().ToString("N"));
    private string Source => Path.Combine(directory, "source.txt");
    private static StorageContext Context => new("account123", new Profile { Name = "native", CloudName = "AzureCloud", TenantId = "tenant" });

    public BlobContentCmdletTests()
    {
        Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(Source, "source");
    }

    [Theory]
    [InlineData("Set")]
    [InlineData("Get")]
    [InlineData("Start")]
    public void WhatIfNeverOpensAnEngineSession(string operation)
    {
        using var fixture = new Invocation();
        var parameters = Parameters(operation);
        parameters["WhatIf"] = true;
        Invoke(fixture, operation, parameters, new Host());
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UploadOverwritePromptRunsOnInvocationThreadAfterSessionDisposalAndDeclineNeverRetries(bool approve)
    {
        using var fixture = new Invocation();
        var host = new Host(approve);
        int? invocationThread = null;
        int? workerThread = null;
        fixture.OnRun = invocation =>
        {
            invocationThread ??= Environment.CurrentManagedThreadId;
            Assert.Equal(invocationThread, Environment.CurrentManagedThreadId);
            if (fixture.Calls.Count == 1)
                ThrowOverwriteFromWorker((StorageBlobPowerShellPlan)invocation.Parameters["Plan"],
                    thread => workerThread = thread);
        };
        host.OnPrompt = () =>
        {
            Assert.NotNull(invocationThread);
            Assert.Equal(invocationThread, Environment.CurrentManagedThreadId);
            Assert.NotEqual(workerThread, Environment.CurrentManagedThreadId);
            Assert.Equal(1, fixture.Disposals);
        };
        Invoke(fixture, "Set", Parameters("Set"), host);
        Assert.Equal(approve ? 2 : 1, fixture.Calls.Count);
        Assert.Equal(1, host.Prompts);
        Assert.Equal(fixture.Calls.Count, fixture.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DownloadOverwriteIsDecidedBeforeOpeningAnySession(bool approve)
    {
        using var fixture = new Invocation();
        var parameters = Parameters("Get");
        parameters["Destination"] = Source;
        var host = new Host(approve) { OnPrompt = () => Assert.Empty(fixture.Calls) };
        Invoke(fixture, "Get", parameters, host);
        Assert.Equal(approve ? 1 : 0, fixture.Calls.Count);
        Assert.Equal(1, host.Prompts);
    }

    [Theory]
    [InlineData("Set")]
    [InlineData("Get")]
    [InlineData("Start")]
    public void DeclinedShouldProcessNeverOpensAnEngineSession(string operation)
    {
        using var fixture = new Invocation();
        var parameters = Parameters(operation);
        parameters["Confirm"] = true;
        var host = new Host(false);
        Invoke(fixture, operation, parameters, host);
        Assert.Equal(1, host.Prompts);
        Assert.Empty(fixture.Calls);
    }

    [Fact]
    public void NativeBlobPipelineUsesItsOwnAddressAndForceIsFrozenInThePlan()
    {
        using var fixture = new Invocation();
        var parameters = new Dictionary<string, object>
        {
            ["File"] = Source, ["Force"] = true, ["Confirm"] = false
        };
        fixture.Invoke<SetAzStorageBlobContentCmdlet>("Set-AzStorageBlobContent", parameters,
            new Host(), [new StorageBlob("native/item", new("container", Context), new())]);
        var plan = Assert.IsType<StorageBlobPowerShellPlan>(Assert.Single(fixture.Calls).Parameters["Plan"]);
        Assert.EndsWith("/container/native/item", plan.Target);
    }

    [Fact]
    public void MalformedPropertiesFailBeforeShouldProcessOrSession()
    {
        using var fixture = new Invocation();
        var parameters = Parameters("Set");
        parameters["Properties"] = new Hashtable { ["ContentMD5"] = "not base64" };
        parameters["WhatIf"] = true;
        Assert.ThrowsAny<RuntimeException>(() => Invoke(fixture, "Set", parameters, new Host()));
        Assert.Empty(fixture.Calls);
    }

    [Theory]
    [InlineData("sas")]
    [InlineData("key")]
    [InlineData("lookup")]
    public void FileCopyAuthorizationBindsAndWhatIfNeverOpensAnEngineSession(string mode)
    {
        using var fixture = new Invocation();
        var parameters = new Dictionary<string, object>
        {
            ["SrcShareName"] = "share", ["SrcFilePath"] = "dir/file", ["Context"] = Context,
            ["DestContainer"] = "destination", ["DestBlob"] = "target", ["WhatIf"] = true
        };
        if (mode == "sas")
        {
            parameters["SourceSas"] = "sv=2026-06-06&sp=r&sig=provided";
            parameters["SourceAccountKey"] = "ignored-invalid-key";
            parameters["UseSourceAccountKeyLookup"] = true;
        }
        else if (mode == "key") parameters["SourceAccountKey"] = Convert.ToBase64String(new byte[32]);
        else
        {
            parameters["UseSourceAccountKeyLookup"] = true; parameters["SourceKeyResourceGroup"] = "source-rg";
            parameters["SourceKeySubscriptionId"] = "55555555-5555-5555-5555-555555555555";
            parameters["SourceKeyName"] = "key2";
        }
        fixture.Invoke<StartAzStorageBlobCopyCmdlet>("Start-AzStorageBlobCopy", parameters, new Host());
        Assert.Empty(fixture.Calls);
    }

    private Dictionary<string, object> Parameters(string operation)
    {
        var result = new Dictionary<string, object> { ["Context"] = Context, ["Confirm"] = false };
        if (operation == "Start")
        {
            result["SrcContainer"] = "source"; result["SrcBlob"] = "item"; result["DestContainer"] = "destination";
            result["DestContext"] = new StorageContext("destination123", Context.Profile);
        }
        else
        {
            result["Container"] = "container"; result["Blob"] = "item";
            if (operation == "Set") result["File"] = Source;
            else result["Destination"] = Path.Combine(directory, "download");
        }
        return result;
    }

    private static void Invoke(Invocation fixture, string operation, Dictionary<string, object> parameters, Host host)
    {
        if (operation == "Set") fixture.Invoke<SetAzStorageBlobContentCmdlet>("Set-AzStorageBlobContent", parameters, host);
        else if (operation == "Get") fixture.Invoke<GetAzStorageBlobContentCmdlet>("Get-AzStorageBlobContent", parameters, host);
        else fixture.Invoke<StartAzStorageBlobCopyCmdlet>("Start-AzStorageBlobCopy", parameters, host);
    }

    private static void ThrowOverwriteFromWorker(StorageBlobPowerShellPlan plan, Action<int> captureThread) =>
        Task.Run(() =>
        {
            captureThread(Environment.CurrentManagedThreadId);
            throw new StorageBlobOverwriteRequiredException(plan);
        }).GetAwaiter().GetResult();

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private sealed class Invocation : IDisposable
    {
        internal List<CommandInvocation> Calls { get; } = [];
        internal int Disposals { get; private set; }
        internal Action<CommandInvocation>? OnRun { get; set; }

        internal Invocation()
        {
            AzclipsServiceProvider.Dispose();
            var engine = new Mock<IEngine>();
            engine.Setup(value => value.OpenExecutionSession(It.IsAny<CommandInvocation>(), It.IsAny<OutputStreams>(),
                It.IsAny<CancellationToken>()))
                .Returns<CommandInvocation, OutputStreams, CancellationToken>((invocation, _, _) =>
                {
                    Calls.Add(invocation);
                    var session = new Mock<ISession>();
                    session.Setup(value => value.Run()).Callback(() => OnRun?.Invoke(invocation));
                    session.Setup(value => value.Dispose()).Callback(() => Disposals++);
                    return session.Object;
                });
            var configuration = new Mock<IConfigurationService>();
            configuration.Setup(value => value.GetConfiguration<CoreOptions>()).Returns(new CoreOptions());
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton(engine.Object); services.AddSingleton(configuration.Object);
                services.AddSingleton(new Mock<ITelemetryService>().Object);
            });
            AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.PowerShell, "1950a258-227b-4e31-a9cf-717495945fc2"));
        }

        internal void Invoke<T>(string name, Dictionary<string, object> parameters, Host host, object[]? input = null) where T : PSCmdlet
        {
            var state = InitialSessionState.Create();
            state.Providers.Add(new SessionStateProviderEntry("FileSystem", typeof(Microsoft.PowerShell.Commands.FileSystemProvider), null));
            state.Commands.Add(new SessionStateCmdletEntry(name, typeof(T), null));
            using var runspace = RunspaceFactory.CreateRunspace(host, state);
            runspace.Open();
            runspace.SessionStateProxy.Path.SetLocation(Environment.CurrentDirectory);
            using var shell = PowerShell.Create();
            shell.Runspace = runspace; shell.AddCommand(name);
            foreach (var parameter in parameters) shell.AddParameter(parameter.Key, parameter.Value);
            if (input is null) shell.Invoke(); else shell.Invoke(input);
            Assert.Empty(shell.Streams.Error);
        }

        public void Dispose() => AzclipsServiceProvider.Dispose();
    }

    private sealed class Host(params bool[] answers) : PSHost
    {
        private readonly UI ui = new(answers);
        internal Action? OnPrompt { get => ui.OnPrompt; set => ui.OnPrompt = value; }
        internal int Prompts => ui.Prompts;
        public override Guid InstanceId { get; } = Guid.NewGuid();
        public override string Name => "Blob content tests";
        public override Version Version => new(1, 0);
        public override PSHostUserInterface UI => ui;
        public override CultureInfo CurrentCulture => CultureInfo.InvariantCulture;
        public override CultureInfo CurrentUICulture => CultureInfo.InvariantCulture;
        public override void EnterNestedPrompt() => throw new NotSupportedException();
        public override void ExitNestedPrompt() => throw new NotSupportedException();
        public override void NotifyBeginApplication() { }
        public override void NotifyEndApplication() { }
        public override void SetShouldExit(int exitCode) => throw new NotSupportedException();
    }

    private sealed class UI(bool[] answers) : PSHostUserInterface
    {
        private readonly Queue<bool> answers = new(answers);
        internal Action? OnPrompt { get; set; }
        internal int Prompts { get; private set; }
        public override PSHostRawUserInterface RawUI => null!;
        public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
        {
            Prompts++; OnPrompt?.Invoke();
            string label = answers.Dequeue() ? "Yes" : "No";
            return choices.Select((choice, index) => (choice, index))
                .Single(pair => pair.choice.Label.Replace("&", "", StringComparison.Ordinal) == label).index;
        }
        public override string ReadLine() => throw new NotSupportedException();
        public override SecureString ReadLineAsSecureString() => throw new NotSupportedException();
        public override Dictionary<string, PSObject> Prompt(string caption, string message, Collection<FieldDescription> descriptions) =>
            throw new NotSupportedException();
        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName) =>
            throw new NotSupportedException();
        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName,
            PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options) => throw new NotSupportedException();
        public override void Write(string value) { }
        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value) { }
        public override void WriteLine(string value) { }
        public override void WriteErrorLine(string value) { }
        public override void WriteDebugLine(string message) { }
        public override void WriteProgress(long sourceId, ProgressRecord record) { }
        public override void WriteVerboseLine(string message) { }
        public override void WriteWarningLine(string message) { }
    }
}
