// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Core.Agent.Tests;

[CollectionDefinition("Troubleshoot", DisableParallelization = true)]
public sealed class TroubleshootCollectionDefinition;

internal static class TroubleshootTestSupport
{
    internal const string Id = "42-2026-0924-0000-0000";

    internal static FailureRecord MakeRecord(uint pid = 42, ulong sequence = 0) => new()
    {
        Id = FailureId.Format(pid, new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero), sequence),
        TerminalSessionId = pid,
        Sequence = sequence,
        OccurredAtUtc = new(2026, 9, 24, 0, 0, 0, TimeSpan.Zero),
        Arguments = ["group", "show", "--name", "test-only-secret"],
        FailedCommand = "group show --name \"test-only-secret\"",
        ErrorText = "original failure with test-only-secret",
        CommandName = "group show",
        ErrorCode = "TestFailure",
        ExitCode = 1,
    };

    internal static AgentOptions Manual() => new() { Recovery = new() { Trigger = "manual" } };

    internal static async Task<List<object>> RunAsync(
        AgentProcessorBase processor, FakeHistory history, FakeConfiguration? configuration = null)
    {
        var configured = configuration ?? new FakeConfiguration(null);
        var written = new List<object>();
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IFailureHistoryStore>(history);
            services.AddSingleton<IConfigurationService>(configured);
        });
        try
        {
            AzclipsServiceProvider.Initialize(new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
            processor.Init(new ProcessorContext(null!, NoOpProxy<IEventPublisher>.Create(), null!, new OutputHandler(written)));
            await processor.ProcessAsync();
            return written;
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }

    private sealed class OutputHandler(List<object> written) : IOutputHandler
    {
        public OutputStreamDelegate WriteOutput { get; set; } = (value, _) =>
        {
            written.Add(value);
            return Task.CompletedTask;
        };
    }

    internal class NoOpProxy<T> : DispatchProxy where T : class
    {
        internal static T Create() => DispatchProxy.Create<T, NoOpProxy<T>>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType == typeof(Task) ? Task.CompletedTask : null;
    }
}

internal sealed class FakeHistory : IFailureHistoryStore
{
    private readonly Func<IReadOnlyList<FailureRecord>> _read;

    internal FakeHistory(params FailureRecord[] records) => _read = () => records;

    internal FakeHistory(Exception error, FailureRecord? latestPlain = null)
    {
        _read = () => throw error;
        LatestPlain = latestPlain;
    }

    internal FailureRecord? LatestPlain { get; }

    internal int Reads { get; private set; }

    public Task<IReadOnlyList<FailureRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default)
    {
        Reads++;
        return Task.FromResult(_read());
    }

    public Task<FailureRecord?> ReadLatestPlainAsync(CancellationToken cancellationToken = default) => Task.FromResult(LatestPlain);
    public Task<FailureHistoryEntry> SaveAsync(FailureRecord record, int maxEntries, CancellationToken cancellationToken = default) => throw Unexpected();
    public Task<IReadOnlyList<FailureHistoryEntry>> ReadIndexAsync(CancellationToken cancellationToken = default) => throw Unexpected();
    public Task ClearAsync(CancellationToken cancellationToken = default) => throw Unexpected();

    private static Xunit.Sdk.XunitException Unexpected() => new("Troubleshoot must only read failure history.");
}

internal sealed class FakeConfiguration(AgentOptions? options) : IConfigurationService
{
    internal int AgentOptionReads { get; private set; }

    public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
    {
        if (typeof(T) != typeof(AgentOptions))
            return new T();
        AgentOptionReads++;
        return options as T ?? throw new Xunit.Sdk.XunitException("Agent options were not expected to be read.");
    }

    public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public string GetConfigFilePath() => throw new NotSupportedException();
    public IConfigurationSchema GetSchema() => throw new NotSupportedException();
}
