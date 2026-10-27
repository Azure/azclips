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
using Core.Runtime;
using CmdletsStorage::Microsoft.Azure.Azclips.Cmdlets.Storage;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public sealed class StorageFileCmdletTests
{
    [Theory]
    [InlineData("ShareObject")]
    [InlineData("ShareClient")]
    public void NativeShareAliasesAndExplicitFalseReachTheTypedLookup(string parameter)
    {
        using var fixture = new Fixture();
        fixture.Invoke<GetAzStorageFileCmdlet>("Get-AzStorageFile", new()
        {
            [parameter] = new StorageFileShare("share", Context()), ["ExcludeExtendedInfo"] = false
        });
        var invocation = Assert.Single(fixture.Invocations);
        Assert.Equal("Get-AzStorageFile", invocation.CommandName);
        Assert.False((bool)invocation.Parameters["ExcludeExtendedInfo"]);
        Assert.IsType<StorageFileDirectory>(invocation.Parameters["DirectoryObject"]);
        Assert.False(invocation.Parameters.ContainsKey("Path"));
        Assert.True((bool)invocation.Parameters["AllowTrailingDot"]);
    }

    [Fact]
    public void ExactDirectoryRelativeLookupFreezesOneResolvedPathWithoutDoublingTheParent()
    {
        using var fixture = new Fixture();
        fixture.Invoke<GetAzStorageFileCmdlet>("Get-AzStorageFile", new()
        {
            ["ShareDirectoryClient"] = new StorageFileDirectory("parent", new StorageFileShare("share", Context())),
            ["Path"] = "child", ["DisAllowTrailingDot"] = true
        });
        var invocation = Assert.Single(fixture.Invocations);
        Assert.Equal("parent/child", invocation.Parameters["Path"]);
        Assert.False(invocation.Parameters.ContainsKey("DirectoryObject"));
        Assert.False((bool)invocation.Parameters["AllowTrailingDot"]);
    }

    [Theory]
    [InlineData("upload")]
    [InlineData("download")]
    [InlineData("directory")]
    public void WhatIfNeverOpensExecutionSessions(string operation)
    {
        using var fixture = new Fixture();
        var parameters = new Dictionary<string, object>
        {
            ["ShareName"] = "share", ["Context"] = Context(), ["WhatIf"] = true
        };
        switch (operation)
        {
            case "upload":
                parameters["Source"] = System.IO.Path.Combine(Environment.CurrentDirectory, "source.txt");
                fixture.Invoke<SetAzStorageFileContentCmdlet>("Set-AzStorageFileContent", parameters);
                break;
            case "download":
                parameters["Path"] = "file.txt";
                parameters["Destination"] = System.IO.Path.Combine(Environment.CurrentDirectory, "download.txt");
                fixture.Invoke<GetAzStorageFileContentCmdlet>("Get-AzStorageFileContent", parameters);
                break;
            default:
                parameters["Path"] = "new";
                fixture.Invoke<NewAzStorageDirectoryCmdlet>("New-AzStorageDirectory", parameters);
                break;
        }
        Assert.Empty(fixture.Invocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UploadConfirmationRunsOnInvocationThreadAfterPreflightSessionDisposal(bool accept)
    {
        using var fixture = new Fixture();
        var context = Context();
        var file = new StorageFile("file.txt", new StorageFileShare("share", context));
        fixture.Run = invocation =>
        {
            if (!invocation.Parameters.ContainsKey("PreflightOnly"))
                Task.Run(() => { throw new StorageFileUploadPreflightException(file, exists: true); }).GetAwaiter().GetResult();
        };
        fixture.Host.Answers.Enqueue(accept);
        fixture.Host.BeforePrompt = () =>
        {
            Assert.True(fixture.Disposals >= 1);
            Assert.Equal(fixture.InvocationThread, Environment.CurrentManagedThreadId);
        };
        fixture.Invoke<SetAzStorageFileContentCmdlet>("Set-AzStorageFileContent", new()
        {
            ["Context"] = context, ["ShareName"] = "share", ["Source"] = "file.txt", ["Confirm"] = false
        });
        Assert.Equal(accept ? 2 : 1, fixture.Invocations.Count);
        Assert.Equal(1, fixture.Host.Prompts);
        if (accept)
        {
            var retry = fixture.Invocations[1];
            Assert.False((bool)retry.Parameters["PreflightOnly"]);
            Assert.Equal("file.txt", retry.Parameters["Path"]);
            Assert.Same(file.Share, retry.Parameters["ShareObject"]);
            Assert.False(retry.Parameters.ContainsKey("DirectoryObject"));
            Assert.False((bool)retry.Parameters["PassThru"]);
        }
    }

    [Fact]
    public void UploadFreezesContentMd5MetadataAndTransferControlsBeforePrompting()
    {
        using var fixture = new Fixture();
        var context = Context();
        byte[] md5 = new byte[16];
        var metadata = new Hashtable { ["owner"] = "before" };
        fixture.Run = invocation =>
        {
            if (!invocation.Parameters.ContainsKey("PreflightOnly"))
                throw new StorageFileUploadPreflightException(new("file.txt", new("share", context)), true);
        };
        fixture.Host.Answers.Enqueue(true);
        fixture.Host.BeforePrompt = () => { md5[0] = 99; metadata["owner"] = "after"; };
        fixture.Invoke<SetAzStorageFileContentCmdlet>("Set-AzStorageFileContent", new()
        {
            ["Context"] = context, ["ShareName"] = "share", ["Source"] = "file.txt", ["Confirm"] = false,
            ["Metadata"] = metadata, ["ContentMD5"] = md5, ["ConcurrentTaskCount"] = 3, ["PassThru"] = true,
            ["Profile"] = "selected"
        });
        var retry = fixture.Invocations[1];
        Assert.Equal(0, ((byte[])retry.Parameters["ContentMd5"])[0]);
        Assert.Equal("before", ((IReadOnlyDictionary<string, string>)retry.Parameters["Metadata"])["owner"]);
        Assert.Equal(3, retry.Parameters["Concurrency"]);
        Assert.True((bool)retry.Parameters["PassThru"]);
        Assert.Equal("selected", retry.Parameters["Profile"]);
    }

    [Fact]
    public void DecliningShouldProcessAfterReadOnlyPreflightNeverStartsTheWriteSession()
    {
        using var fixture = new Fixture();
        var context = Context();
        fixture.Run = _ => throw new StorageFileUploadPreflightException(new("file.txt", new("share", context)), false);
        fixture.Host.Answers.Enqueue(false);
        fixture.Invoke<SetAzStorageFileContentCmdlet>("Set-AzStorageFileContent", new()
        {
            ["Context"] = context, ["ShareName"] = "share", ["Source"] = "file.txt", ["Confirm"] = true
        });
        Assert.Single(fixture.Invocations);
        Assert.Equal(1, fixture.Host.Prompts);
    }

    [Fact]
    public void FilePipelineDownloadResolvesAnExistingLocalDirectoryAndRequiresNoWorkerCallback()
    {
        using var fixture = new Fixture();
        var file = new StorageFile("remote/file.txt", new("share", Context()));
        fixture.Invoke<GetAzStorageFileContentCmdlet>("Get-AzStorageFileContent", new()
        {
            ["Destination"] = Environment.CurrentDirectory, ["Force"] = true, ["CheckMd5"] = true,
            ["PassThru"] = true, ["Confirm"] = false
        }, [file]);
        var invocation = Assert.Single(fixture.Invocations);
        Assert.Equal("remote/file.txt", invocation.Parameters["Path"]);
        Assert.Equal(System.IO.Path.Combine(Environment.CurrentDirectory, "file.txt"), invocation.Parameters["Destination"]);
        Assert.True((bool)invocation.Parameters["Overwrite"]);
        Assert.True((bool)invocation.Parameters["CheckMd5"]);
        Assert.True((bool)invocation.Parameters["PassThru"]);
        Assert.DoesNotContain(invocation.Parameters.Values, value => value is Delegate);
    }

    [Fact]
    public void NewDirectoryUsesNativeParentAndOnlyOneShouldProcessScope()
    {
        using var fixture = new Fixture();
        fixture.Invoke<NewAzStorageDirectoryCmdlet>("New-AzStorageDirectory", new()
        {
            ["DirectoryObject"] = new StorageFileDirectory("parent", new("share", Context())),
            ["Path"] = "new", ["Confirm"] = false
        });
        Assert.Equal("parent/new", Assert.Single(fixture.Invocations).Parameters["Path"]);
    }

    [Fact]
    public void UnrelatedBackendFailureRemainsTerminatingAndIsNotRetried()
    {
        using var fixture = new Fixture();
        fixture.Run = _ => throw new HttpRequestException("write failure");
        Assert.Throws<CmdletInvocationException>(() => fixture.Invoke<SetAzStorageFileContentCmdlet>("Set-AzStorageFileContent", new()
        {
            ["Context"] = Context(), ["ShareName"] = "share", ["Source"] = "file.txt", ["Confirm"] = false
        }));
        Assert.Single(fixture.Invocations);
    }

    private static StorageContext Context() => new("account123",
        new Profile { Name = "selected", TenantId = "tenant", CloudName = "AzureCloud" }, enableFileBackupRequestIntent: true);

    private sealed class Fixture : IDisposable
    {
        internal List<CommandInvocation> Invocations { get; } = [];
        internal Action<CommandInvocation>? Run { get; set; }
        internal int Disposals { get; private set; }
        internal int InvocationThread { get; private set; }
        internal ConfirmationHost Host { get; } = new();

        internal Fixture()
        {
            AzclipsServiceProvider.Dispose();
            var engine = new Mock<IEngine>();
            engine.Setup(value => value.OpenExecutionSession(It.IsAny<CommandInvocation>(), It.IsAny<OutputStreams>(),
                It.IsAny<CancellationToken>())).Returns((CommandInvocation invocation, OutputStreams _, CancellationToken _) =>
            {
                Invocations.Add(invocation);
                InvocationThread = Environment.CurrentManagedThreadId;
                var session = new Mock<ISession>();
                session.Setup(value => value.Run()).Callback(() => Run?.Invoke(invocation));
                session.Setup(value => value.Dispose()).Callback(() => Disposals++);
                return session.Object;
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

        internal void Invoke<T>(string name, Dictionary<string, object> parameters, object[]? input = null) where T : PSCmdlet
        {
            var state = InitialSessionState.Create();
            state.Providers.Add(new SessionStateProviderEntry("FileSystem", typeof(Microsoft.PowerShell.Commands.FileSystemProvider), null));
            state.Commands.Add(new SessionStateCmdletEntry(name, typeof(T), null));
            using var runspace = RunspaceFactory.CreateRunspace(Host, state);
            runspace.Open();
            runspace.SessionStateProxy.Path.SetLocation(Environment.CurrentDirectory);
            using var shell = PowerShell.Create();
            shell.Runspace = runspace;
            shell.AddCommand(name);
            foreach (var parameter in parameters) shell.AddParameter(parameter.Key, parameter.Value);
            if (input is null) shell.Invoke();
            else shell.Invoke(input);
            Assert.Empty(shell.Streams.Error);
        }

        public void Dispose() => AzclipsServiceProvider.Dispose();
    }

    private sealed class ConfirmationHost : PSHost
    {
        private readonly ConfirmationUi ui = new();
        internal Queue<bool> Answers => ui.Answers;
        internal int Prompts => ui.Prompts;
        internal Action? BeforePrompt { get => ui.BeforePrompt; set => ui.BeforePrompt = value; }
        public override Guid InstanceId { get; } = Guid.NewGuid();
        public override string Name => "Files frontend tests";
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

    private sealed class ConfirmationUi : PSHostUserInterface
    {
        internal Queue<bool> Answers { get; } = new();
        internal int Prompts { get; private set; }
        internal Action? BeforePrompt { get; set; }
        public override PSHostRawUserInterface RawUI => null!;
        public override int PromptForChoice(string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
        {
            Prompts++;
            BeforePrompt?.Invoke();
            string answer = Answers.Dequeue() ? "Yes" : "No";
            return choices.Select((choice, index) => (choice, index))
                .Single(pair => pair.choice.Label.Replace("&", "", StringComparison.Ordinal) == answer).index;
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
}
