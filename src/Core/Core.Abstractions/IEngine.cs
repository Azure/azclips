// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;

namespace Core.Abstractions;

public interface IEngine : IDisposable
{
    ISession OpenExecutionSession(CommandInvocation invocation, OutputStreams outputStreams, CancellationToken cancellationToken = default);

    ISession OpenDiscoverySession(IList<string> extensionNames, string? commandName, OutputStreams outputStreams, bool isTransient = false, CancellationToken cancellationToken = default);
}

public interface ICommandEngine
{
    /// <summary>
    /// Gets the last extracted PowerShell command (if applicable)
    /// </summary>
    string? ExtractedCommand { get; }

    Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default);
}