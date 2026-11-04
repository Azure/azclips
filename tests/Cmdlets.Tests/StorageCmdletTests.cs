// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsStorage;

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Host;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security;
using Core.Abstractions;
using Core.ArgumentParsing;
using Core.DependencyInjection;
using Core.Runtime;
using Core.Runtime.Persistence;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using CmdletsStorage::Microsoft.Azure.Azclips.Cmdlets.Storage;
using Microsoft.Azure.Azclips.Extensions.Storage;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public sealed class StorageCmdletTests
{

    [Fact]
    public void OmittedContextSwitchesPreserveOAuthAndNoBackupIntentDefaults()
    {
        using var fixture = new InvocationFixture();
        fixture.Invoke<NewAzStorageContextCmdlet>("New-AzStorageContext", new() { ["StorageAccountName"] = "account123" });
        var invocation = Assert.IsType<CommandInvocation>(fixture.Invocation);
        Assert.Single(invocation.Parameters);
        var processor = new NewStorageContextProcessor();
        Bind(processor, invocation);
        Assert.Equal("account123", processor.StorageAccountName);
        Assert.True(processor.UseConnectedAccount);
        Assert.False(processor.EnableFileBackupRequestIntent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitContextSwitchValuesAreNotDropped(bool enabled)
    {
        using var fixture = new InvocationFixture();
        fixture.Invoke<NewAzStorageContextCmdlet>("New-AzStorageContext", new()
        {
            ["StorageAccountName"] = "account123", ["UseConnectedAccount"] = enabled,
            ["EnableFileBackupRequestIntent"] = enabled
        });
        var processor = new NewStorageContextProcessor();
        Bind(processor, Assert.IsType<CommandInvocation>(fixture.Invocation));
        Assert.Equal(enabled, processor.UseConnectedAccount);
        Assert.Equal(enabled, processor.EnableFileBackupRequestIntent);
    }


    [Fact]
    public void EngineFailuresRemainTerminatingWithoutSuccessOutput()
    {
        using var fixture = new InvocationFixture();
        fixture.Session.Setup(session => session.Run()).Throws(new InvalidOperationException("fixture failure"));
        Assert.Throws<CmdletInvocationException>(() =>
            fixture.Invoke<NewAzStorageContextCmdlet>("New-AzStorageContext", new() { ["StorageAccountName"] = "account123" }));
        fixture.Session.Verify(session => session.Dispose(), Times.Once);
    }

    [Theory]
    [InlineData("Container")]
    [InlineData("Name")]
    [InlineData("N")]
    public void ExactBlobLookupUsesNativeContextAndContainerAliases(string containerParameter)
    {
        using var fixture = new InvocationFixture();
        var context = NativeContext();
        fixture.Invoke<GetAzStorageBlobCmdlet>("Get-AzStorageBlob", new()
        {
            [containerParameter] = "container", ["Blob"] = "literal/name", ["Context"] = context,
            ["IncludeTag"] = true, ["TagCondition"] = "\"team\"='one'", ["ServerTimeoutPerRequest"] = 9
        });
        var processor = BindBlob(fixture);
        Assert.False(processor.ListMode);
        Assert.Same(context, processor.Context);
        Assert.Equal("literal/name", processor.Blob);
        Assert.Equal("container", processor.Container);
        Assert.True(processor.IncludeTag);
        Assert.Equal("\"team\"='one'", processor.IfTags);
        Assert.Equal(9, processor.Timeout);
        Assert.Equal(int.MaxValue, processor.MaxCount);
        Assert.Null(processor.NameFilter);
    }

    [Theory]
    [InlineData(null, "", true, true)]
    [InlineData("", "", true, true)]
    [InlineData("dir/a?[0-9].TXT", "dir/a", true, false)]
    [InlineData("dir/*", "dir/", true, true)]
    public void WildcardListingUsesPowerShellSemantics(string? pattern, string prefix, bool firstMatches, bool secondMatches)
    {
        using var fixture = new InvocationFixture();
        var parameters = new Dictionary<string, object> { ["Container"] = "container", ["Context"] = NativeContext() };
        if (pattern is not null) parameters.Add("Blob", pattern);
        fixture.Invoke<GetAzStorageBlobCmdlet>("Get-AzStorageBlob", parameters);
        var processor = BindBlob(fixture);
        Assert.True(processor.ListMode);
        Assert.Equal(prefix, processor.Prefix);
        Assert.Equal(firstMatches, processor.NameFilter?.Invoke("dir/Ab4.txt") ?? true);
        Assert.Equal(secondMatches, processor.NameFilter?.Invoke("dir/other") ?? true);
        Assert.Equal(int.MaxValue, processor.MaxCount);
        Assert.Null(processor.Blob);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public void PrefixAndContinuationAreLiteralAndExplicitFalseIsPreserved(int count)
    {
        using var fixture = new InvocationFixture();
        var token = new StorageBlobContinuationToken("opaque", "account123", "container", "literal[*]", null);
        fixture.Invoke<GetAzStorageBlobCmdlet>("Get-AzStorageBlob", new()
        {
            ["Context"] = NativeContext(), ["Container"] = "container", ["Prefix"] = "literal[*]",
            ["MaxCount"] = count, ["ContinuationToken"] = token, ["IncludeVersion"] = true,
            ["IncludeDeleted"] = false, ["IncludeTag"] = false
        });
        var processor = BindBlob(fixture);
        Assert.True(processor.ListMode);
        Assert.Equal("literal[*]", processor.Prefix);
        Assert.Null(processor.NameFilter);
        Assert.Equal(count, processor.MaxCount);
        Assert.Same(token, processor.ContinuationToken);
        Assert.True(processor.IncludeVersion);
        Assert.Equal(false, fixture.Invocation!.Parameters["IncludeDeleted"]);
        Assert.Equal(false, fixture.Invocation.Parameters["IncludeTag"]);
    }

    [Fact]
    public void IncludeDeletedTurnsAnExactNameIntoFilteredListing()
    {
        using var fixture = new InvocationFixture();
        fixture.Invoke<GetAzStorageBlobCmdlet>("Get-AzStorageBlob", new()
        {
            ["Context"] = NativeContext(), ["Container"] = "container", ["Blob"] = "item", ["IncludeDeleted"] = true
        });
        var processor = BindBlob(fixture);
        Assert.True(processor.ListMode);
        Assert.True(processor.IncludeDeleted);
        Assert.Equal("item", processor.Prefix);
        Assert.True(processor.NameFilter!("ITEM"));
        Assert.False(processor.NameFilter("item-more"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotAndVersionSetsSelectExactLookup(bool snapshot)
    {
        using var fixture = new InvocationFixture();
        var parameters = new Dictionary<string, object>
        {
            ["Context"] = NativeContext(), ["Container"] = "container", ["Blob"] = "item", ["IncludeTag"] = true
        };
        parameters.Add(snapshot ? "SnapshotTime" : "VersionId", snapshot
            ? DateTimeOffset.Parse("2026-10-21T01:02:03+02:00") : "version");
        fixture.Invoke<GetAzStorageBlobCmdlet>("Get-AzStorageBlob", parameters);
        var processor = BindBlob(fixture);
        Assert.False(processor.ListMode);
        Assert.Equal(snapshot ? "2026-10-20T23:02:03.0000000Z" : null, processor.Snapshot);
        Assert.Equal(snapshot ? null : "version", processor.VersionId);
        Assert.True(processor.IncludeTag);
    }

    [Fact]
    public void NativeContainerPipelineSelectsAVersion()
    {
        using var fixture = new InvocationFixture();
        var container = new StorageBlobContainer("container", NativeContext());
        var parameters = new Dictionary<string, object> { ["Blob"] = "item", ["VersionId"] = "version" };
        fixture.Invoke<GetAzStorageBlobCmdlet>("Get-AzStorageBlob", parameters, [container]);
        var processor = BindBlob(fixture);
        Assert.Same(container, processor.ContainerObject);
        Assert.False(processor.ListMode);
        Assert.Null(processor.Context);
        Assert.Null(processor.Container);
    }

    [Theory]
    [InlineData("TagCondition", "\"tag\"='value'", true)]
    [InlineData("Prefix", "prefix", false)]
    [InlineData("IncludeVersion", true, false)]
    [InlineData("ServerTimeoutPerRequest", 0, false)]
    public void ConflictingOrInvalidOptionsNeverInvokeTheEngine(string option, object value, bool wildcard)
    {
        using var fixture = new InvocationFixture();
        Assert.ThrowsAny<RuntimeException>(() => fixture.Invoke<GetAzStorageBlobCmdlet>("Get-AzStorageBlob", new()
        {
            ["Context"] = NativeContext(), ["Container"] = "container", ["Blob"] = wildcard ? "*" : "item", [option] = value
        }));
        Assert.Empty(fixture.Invocations);
    }

    [Fact]
    public void ExactNameLookupRetainsLegacyIgnoreBehaviorForListingWindowControls()
    {
        using var fixture = new InvocationFixture();
        fixture.Invoke<GetAzStorageBlobCmdlet>("Get-AzStorageBlob", new()
        {
            ["Context"] = NativeContext(), ["Container"] = "container", ["Blob"] = "item",
            ["MaxCount"] = 1, ["ContinuationToken"] = new StorageBlobContinuationToken("unused", "account123", "container", null, null),
            ["IncludeDeleted"] = false, ["IncludeTag"] = false
        });
        var processor = BindBlob(fixture);
        Assert.False(processor.ListMode);
        Assert.Equal(int.MaxValue, processor.MaxCount);
        Assert.Null(processor.ContinuationToken);
        Assert.Equal(false, fixture.Invocation!.Parameters["IncludeDeleted"]);
        Assert.Equal(false, fixture.Invocation.Parameters["IncludeTag"]);
    }

    [Theory]
    [InlineData("IncludeDeleted", true)]
    [InlineData("MaxCount", 1)]
    [InlineData("VersionId", "version")]
    public void SnapshotSetRejectsConflictingSelectorsAndListingControls(string option, object value)
    {
        using var fixture = new InvocationFixture();
        Assert.ThrowsAny<RuntimeException>(() => fixture.Invoke<GetAzStorageBlobCmdlet>("Get-AzStorageBlob", new()
        {
            ["Context"] = NativeContext(), ["Container"] = "container", ["Blob"] = "item",
            ["SnapshotTime"] = DateTimeOffset.Parse("2026-10-20T00:00:00Z"), [option] = value
        }));
        Assert.Empty(fixture.Invocations);
    }

    private static StorageContext NativeContext() => new("account123",
        new Profile { Name = "selected", TenantId = "tenant", CloudName = "AzureCloud" });

    [Theory]
    [InlineData(false, false, false, null)]
    [InlineData(true, false, false, "include")]
    [InlineData(false, true, false, "only")]
    [InlineData(true, true, false, "only")]
    [InlineData(false, false, true, null)]
    [InlineData(true, false, true, null)]
    public void RemovalSwitchesSelectTheApprovedScope(bool force, bool snapshotsOnly, bool selected, string? policy)
    {
        using var fixture = new InvocationFixture { Output = [] };
        var parameters = RemovalParameters();
        parameters.Add("Force", force); parameters.Add("DeleteSnapshot", snapshotsOnly); parameters.Add("PassThru", false);
        parameters.Add("TagCondition", "\"team\"='one'"); parameters.Add("ServerTimeoutPerRequest", 9);
        if (selected) parameters.Add("VersionId", "version");
        var host = new ConfirmationHost();
        var output = fixture.Invoke<RemoveAzStorageBlobCmdlet>("Remove-AzStorageBlob", parameters, host: host);
        var processor = BindRemoval(fixture);
        Assert.Equal(policy, processor.DeleteSnapshots);
        Assert.Equal(selected ? "version" : null, processor.VersionId);
        Assert.Equal("\"team\"='one'", processor.IfTags);
        Assert.Equal(9, processor.Timeout);
        Assert.False(processor.PassThru);
        Assert.Equal(false, fixture.Invocation!.Parameters["PassThru"]);
        Assert.DoesNotContain("Force", fixture.Invocation.Parameters.Keys);
        Assert.DoesNotContain("Confirm", fixture.Invocation.Parameters.Keys);
        Assert.Empty(output);
        Assert.Empty(host.Prompts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovalWhatIfNeverOpensAnEngineSessionEvenWithForce(bool force)
    {
        using var fixture = new InvocationFixture { Output = [] };
        var parameters = RemovalParameters();
        parameters.Add("WhatIf", true); parameters.Add("Force", force); parameters.Add("PassThru", true);
        var host = new ConfirmationHost();
        Assert.Empty(fixture.Invoke<RemoveAzStorageBlobCmdlet>("Remove-AzStorageBlob", parameters, host: host));
        Assert.Empty(fixture.Invocations);
        Assert.Empty(host.Prompts);
    }

    [Fact]
    public void DecliningShouldProcessPreventsDeletionEvenWithForce()
    {
        using var fixture = new InvocationFixture { Output = [] };
        var parameters = RemovalParameters();
        parameters["Confirm"] = true; parameters.Add("Force", true);
        var host = new ConfirmationHost(false);
        Assert.Empty(fixture.Invoke<RemoveAzStorageBlobCmdlet>("Remove-AzStorageBlob", parameters, host: host));
        Assert.Empty(fixture.Invocations);
        Assert.Single(host.Prompts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotConflictConfirmationRunsAfterDisposalOnTheInvocationThread(bool approve)
    {
        using var fixture = new InvocationFixture { Output = [true] };
        var context = NativeContext();
        var container = new StorageBlobContainer("container", context);
        int thread = 0;
        fixture.BeforeRun = () =>
        {
            thread = Environment.CurrentManagedThreadId;
            if (fixture.Invocations.Count == 1) throw new StorageBlobSnapshotsPresentException(container, "item");
        };
        var host = new ConfirmationHost(approve);
        host.OnPrompt = () =>
        {
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            fixture.Session.Verify(session => session.Dispose(), Times.Once);
            Assert.Single(fixture.Invocations);
        };
        var parameters = RemovalParameters();
        parameters.Add("PassThru", true); parameters.Add("Profile", "selected");
        parameters.Add("TagCondition", "\"team\"='one'"); parameters.Add("ServerTimeoutPerRequest", 9);
        var output = fixture.Invoke<RemoveAzStorageBlobCmdlet>("Remove-AzStorageBlob", parameters, host: host);
        Assert.Single(host.Prompts);
        Assert.Equal("Blob snapshot removal", host.Prompts[0].Caption);
        Assert.Contains("https://account123.blob.core.windows.net/container/item", host.Prompts[0].Message);
        Assert.Equal(approve ? 2 : 1, fixture.Invocations.Count);
        if (approve)
        {
            Assert.True(Assert.IsType<bool>(Assert.Single(output).BaseObject));
            var retry = fixture.Invocations[1];
            Assert.Same(context, retry.Parameters["Context"]);
            Assert.Same(container, retry.Parameters["ContainerObject"]);
            Assert.Equal("item", retry.Parameters["Blob"]);
            Assert.Equal("include", retry.Parameters["DeleteSnapshots"]);
            Assert.Equal("\"team\"='one'", retry.Parameters["IfTags"]);
            Assert.Equal(9, retry.Parameters["Timeout"]);
            Assert.Equal("selected", retry.Parameters["Profile"]);
            Assert.DoesNotContain("Container", retry.Parameters.Keys);
            Assert.DoesNotContain("InputObject", retry.Parameters.Keys);
            fixture.Session.Verify(session => session.Dispose(), Times.Exactly(2));
        }
        else
        {
            Assert.Empty(output);
            fixture.Session.Verify(session => session.Dispose(), Times.Once);
        }
    }

    [Fact]
    public void ConfirmApprovalStillRequiresSeparateSnapshotApproval()
    {
        using var fixture = new InvocationFixture { Output = [] };
        fixture.BeforeRun = () => throw new StorageBlobSnapshotsPresentException(new("container", NativeContext()), "item");
        var parameters = RemovalParameters(); parameters["Confirm"] = true;
        var host = new ConfirmationHost(true, false);
        Assert.Empty(fixture.Invoke<RemoveAzStorageBlobCmdlet>("Remove-AzStorageBlob", parameters, host: host));
        Assert.Single(fixture.Invocations);
        Assert.Equal(2, host.Prompts.Count);
        Assert.Equal("Blob snapshot removal", host.Prompts[1].Caption);
    }

    [Fact]
    public void AFailedConfirmedRetryIsNotSuccessAndCannotPromptAgain()
    {
        using var fixture = new InvocationFixture { Output = [true] };
        fixture.BeforeRun = () =>
        {
            if (fixture.Invocations.Count == 1)
                throw new StorageBlobSnapshotsPresentException(new("container", NativeContext()), "item");
            throw new HttpRequestException("retry failed", null, System.Net.HttpStatusCode.Forbidden);
        };
        var host = new ConfirmationHost(true);
        var parameters = RemovalParameters(); parameters.Add("PassThru", true);
        Assert.Throws<CmdletInvocationException>(() =>
            fixture.Invoke<RemoveAzStorageBlobCmdlet>("Remove-AzStorageBlob", parameters, host: host));
        Assert.Equal(2, fixture.Invocations.Count);
        Assert.Single(host.Prompts);
        fixture.Session.Verify(session => session.Dispose(), Times.Exactly(2));
    }

    [Fact]
    public void UnrelatedEngineFailureDoesNotRequestConfirmation()
    {
        using var fixture = new InvocationFixture();
        fixture.BeforeRun = () => throw new HttpRequestException("lease conflict", null, System.Net.HttpStatusCode.Conflict);
        var host = new ConfirmationHost();
        Assert.Throws<CmdletInvocationException>(() =>
            fixture.Invoke<RemoveAzStorageBlobCmdlet>("Remove-AzStorageBlob", RemovalParameters(), host: host));
        Assert.Single(fixture.Invocations);
        Assert.Empty(host.Prompts);
    }

    [Fact]
    public async Task StoppingDuringSnapshotConfirmationPreventsTheRetry()
    {
        using var fixture = new InvocationFixture { Output = [true] };
        fixture.BeforeRun = () => throw new StorageBlobSnapshotsPresentException(new("container", NativeContext()), "item");
        Task? stopping = null;
        var host = new ConfirmationHost(true);
        host.OnPrompt = () =>
        {
            stopping = fixture.PendingStop = Task.Run(() => fixture.ActiveShell!.Stop());
            Assert.True(SpinWait.SpinUntil(() => fixture.ExecutionToken.IsCancellationRequested, TimeSpan.FromSeconds(5)));
        };
        Assert.Empty(fixture.Invoke<RemoveAzStorageBlobCmdlet>("Remove-AzStorageBlob", RemovalParameters(), host: host));
        Assert.NotNull(stopping);
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(PSInvocationState.Stopped, fixture.LastState);
        Assert.True(fixture.ExecutionToken.IsCancellationRequested);
        Assert.Single(fixture.Invocations);
        fixture.Session.Verify(session => session.Dispose(), Times.Once);
    }

    [Fact]
    public void RemovalSnapshotTimeIsUtc()
    {
        using var fixture = new InvocationFixture { Output = [] };
        var parameters = RemovalParameters();
        parameters.Add("SnapshotTime", DateTimeOffset.Parse("2026-10-21T01:02:03+02:00"));
        fixture.Invoke<RemoveAzStorageBlobCmdlet>("Remove-AzStorageBlob", parameters, host: new ConfirmationHost());
        Assert.Equal("2026-10-20T23:02:03.0000000Z", BindRemoval(fixture).Snapshot);
    }

    [Theory]
    [InlineData("selectors")]
    [InlineData("selected-snapshots-only")]
    [InlineData("native-snapshots-only")]
    [InlineData("snapshot-null")]
    [InlineData("context")]
    [InlineData("timeout")]
    public void InvalidRemovalInputsFailBeforeConfirmationOrEngineInvocation(string invalid)
    {
        using var fixture = new InvocationFixture();
        var parameters = RemovalParameters();
        if (invalid == "selectors") { parameters.Add("VersionId", "version"); parameters.Add("SnapshotTime", DateTimeOffset.UtcNow); }
        if (invalid == "selected-snapshots-only") { parameters.Add("VersionId", "version"); parameters.Add("DeleteSnapshot", true); }
        if (invalid is "context" or "native-snapshots-only")
        {
            parameters.Remove("Blob"); parameters.Remove("Container");
            parameters.Add("InputObject", new StorageBlob("item", new("container",
                invalid == "context" ? new("other", NativeContext().Profile) : NativeContext()), new())
            {
                Snapshot = invalid == "native-snapshots-only" ? "snapshot" : null
            });
            if (invalid == "native-snapshots-only") parameters.Add("DeleteSnapshot", true);
        }
        if (invalid == "timeout") parameters.Add("ServerTimeoutPerRequest", 0);
        if (invalid == "snapshot-null") parameters.Add("SnapshotTime", null!);
        var host = new ConfirmationHost();
        Assert.ThrowsAny<RuntimeException>(() =>
            fixture.Invoke<RemoveAzStorageBlobCmdlet>("Remove-AzStorageBlob", parameters, host: host));
        Assert.Empty(fixture.Invocations);
        Assert.Empty(host.Prompts);
    }

    private static Dictionary<string, object> RemovalParameters() => new()
    {
        ["Context"] = NativeContext(), ["Container"] = "container", ["Blob"] = "item", ["Confirm"] = false
    };

    private static RemoveStorageBlobPowerShellCommand BindRemoval(InvocationFixture fixture)
    {
        var processor = new RemoveStorageBlobPowerShellCommand();
        Bind(processor, Assert.IsType<CommandInvocation>(fixture.Invocation));
        return processor;
    }

    private static GetStorageBlobsPowerShellCommand BindBlob(InvocationFixture fixture)
    {
        var processor = new GetStorageBlobsPowerShellCommand();
        Bind(processor, Assert.IsType<CommandInvocation>(fixture.Invocation));
        return processor;
    }

    private static void Bind(IProcessor processor, CommandInvocation invocation)
    {
        var metadata = new ExtensionIndex().GetCommandMetadata(invocation.CommandName, ClientType.PowerShell);
        var binder = new ArgumentParsingService(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));
        binder.BindArgumentsToProcessor(processor, invocation.Parameters, metadata,
            new Profile { Name = "selected", TenantId = "tenant", SubscriptionId = "11111111-1111-1111-1111-111111111111" });
    }

    private sealed class InvocationFixture : IDisposable
    {
        internal Mock<ISession> Session { get; } = new();
        internal CommandInvocation? Invocation { get; private set; }
        internal List<CommandInvocation> Invocations { get; } = [];
        internal object[] Output { get; set; } = ["first", "second"];
        internal Action? BeforeRun { get; set; }
        internal CancellationToken ExecutionToken { get; private set; }
        internal PowerShell? ActiveShell { get; private set; }
        internal PSInvocationState? LastState { get; private set; }
        internal Task? PendingStop { get; set; }

        internal InvocationFixture()
        {
            AzclipsServiceProvider.Dispose();
            var engine = new Mock<IEngine>();
            OutputStreams? streams = null;
            engine.Setup(value => value.OpenExecutionSession(It.IsAny<CommandInvocation>(), It.IsAny<OutputStreams>(),
                It.IsAny<CancellationToken>()))
                .Callback<CommandInvocation, OutputStreams, CancellationToken>((invocation, output, token) =>
                {
                    Invocation = invocation;
                    Invocations.Add(invocation);
                    streams = output;
                    Assert.True(token.CanBeCanceled);
                    ExecutionToken = token;
                }).Returns(Session.Object);
            Session.Setup(value => value.Run()).Callback(() =>
            {
                BeforeRun?.Invoke();
                streams!.WriteOutput(Output, CancellationToken.None).GetAwaiter().GetResult();
            });
            var configuration = new Mock<IConfigurationService>();
            configuration.Setup(value => value.GetConfiguration<CoreOptions>()).Returns(new CoreOptions());
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton(engine.Object);
                services.AddSingleton(configuration.Object);
                services.AddSingleton(new Mock<ITelemetryService>().Object);
            });
            AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.PowerShell, "1950a258-227b-4e31-a9cf-717495945fc2"));
        }

        internal System.Collections.ObjectModel.Collection<PSObject> Invoke<T>(string command, Dictionary<string, object> parameters,
            object[]? input = null, ConfirmationHost? host = null)
            where T : PSCmdlet
        {
            var state = InitialSessionState.Create();
            state.Commands.Add(new SessionStateCmdletEntry(command, typeof(T), null));
            using var runspace = host is null ? null : RunspaceFactory.CreateRunspace(host, state);
            runspace?.Open();
            using var shell = host is null ? PowerShell.Create(state) : PowerShell.Create();
            ActiveShell = shell;
            if (runspace is not null) shell.Runspace = runspace;
            shell.AddCommand(command);
            foreach (var parameter in parameters) shell.AddParameter(parameter.Key, parameter.Value);
            var output = input is null ? shell.Invoke() : shell.Invoke(input);
            PendingStop?.Wait(TimeSpan.FromSeconds(5));
            LastState = shell.InvocationStateInfo.State;
            Assert.Empty(shell.Streams.Error);
            return output;
        }

        public void Dispose() => AzclipsServiceProvider.Dispose();
    }

    private sealed class ConfirmationHost(params bool[] answers) : PSHost
    {
        private readonly ConfirmationUI ui = new(answers);
        internal List<(string Caption, string Message)> Prompts => ui.Prompts;
        internal Action? OnPrompt { get => ui.OnPrompt; set => ui.OnPrompt = value; }
        public override Guid InstanceId { get; } = Guid.NewGuid();
        public override string Name => "Storage confirmation test host";
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

    private sealed class ConfirmationUI(bool[] answers) : PSHostUserInterface
    {
        private readonly Queue<bool> answers = new(answers);
        internal List<(string Caption, string Message)> Prompts { get; } = [];
        internal Action? OnPrompt { get; set; }
        public override PSHostRawUserInterface RawUI { get; } = new ConfirmationRawUI();
        public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
        {
            Prompts.Add((caption, message));
            OnPrompt?.Invoke();
            if (answers.Count == 0) throw new InvalidOperationException("Unexpected confirmation prompt.");
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
        public override void WriteDebugLine(string message) { }
        public override void WriteErrorLine(string value) { }
        public override void WriteProgress(long sourceId, ProgressRecord record) { }
        public override void WriteVerboseLine(string message) { }
        public override void WriteWarningLine(string message) { }
    }

    private sealed class ConfirmationRawUI : PSHostRawUserInterface
    {
        public override ConsoleColor BackgroundColor { get; set; } = ConsoleColor.Black;
        public override ConsoleColor ForegroundColor { get; set; } = ConsoleColor.White;
        public override Size BufferSize { get; set; } = new(120, 40);
        public override Size WindowSize { get; set; } = new(120, 40);
        public override Coordinates CursorPosition { get; set; }
        public override Coordinates WindowPosition { get; set; }
        public override int CursorSize { get; set; } = 1;
        public override bool KeyAvailable => false;
        public override Size MaxPhysicalWindowSize => new(120, 40);
        public override Size MaxWindowSize => new(120, 40);
        public override string WindowTitle { get; set; } = "Storage confirmation tests";
        public override void FlushInputBuffer() => throw new NotSupportedException();
        public override BufferCell[,] GetBufferContents(Rectangle rectangle) => throw new NotSupportedException();
        public override KeyInfo ReadKey(ReadKeyOptions options) => throw new NotSupportedException();
        public override void ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill) =>
            throw new NotSupportedException();
        public override void SetBufferContents(Coordinates origin, BufferCell[,] contents) => throw new NotSupportedException();
        public override void SetBufferContents(Rectangle rectangle, BufferCell fill) => throw new NotSupportedException();
    }
}
