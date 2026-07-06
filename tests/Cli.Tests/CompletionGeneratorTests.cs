// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Completions;

namespace Cli.Tests;

public class CompletionGeneratorTests
{
    [Fact]
    public void ResolveEnabled_ReturnsTrue_WhenCompletionEnabled()
    {
        var config = new StubConfigurationService(new CompletionOptions { Enabled = true });

        Assert.True(CompletionGenerator.ResolveEnabled(config));
    }

    [Fact]
    public void ResolveEnabled_ReturnsFalse_WhenCompletionDisabled()
    {
        var config = new StubConfigurationService(new CompletionOptions { Enabled = false });

        Assert.False(CompletionGenerator.ResolveEnabled(config));
    }

    [Fact]
    public void ResolveEnabled_FailsSafeToEnabled_WhenConfigThrows()
    {
        var config = new ThrowingConfigurationService();

        // A benign, default-on convenience feature must not break because the
        // config file is temporarily unreadable — fail safe to enabled.
        Assert.True(CompletionGenerator.ResolveEnabled(config));
    }

    //TODO: rewrite with mocking framework when introduced.
    private sealed class StubConfigurationService(CompletionOptions completion) : IConfigurationService
    {
        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
        {
            if (typeof(T) == typeof(CompletionOptions))
            {
                return (T)(object)completion;
            }

            return defaultValue ?? new T();
        }

        public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string GetConfigFilePath() => throw new NotSupportedException();
        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }

    private sealed class ThrowingConfigurationService : IConfigurationService
    {
        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
            => throw new InvalidOperationException("config file contains invalid JSON");

        public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string GetConfigFilePath() => throw new NotSupportedException();
        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }
}
