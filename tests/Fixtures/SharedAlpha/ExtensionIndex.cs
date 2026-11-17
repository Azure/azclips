// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Azclips.Tests.Fixtures.SharedAlpha;

/// <summary>
/// Test fixture: declares the verb <c>sharedverb</c> and a single leaf
/// <c>sharedverb alpha</c>. Used by the shared-verb contract test to verify that two extensions
/// sharing a verb but contributing disjoint leaves can co-exist (paired with SharedBeta) and
/// that two extensions sharing the same <c>(verb, path)</c> tuple collide (paired with itself
/// staged under a different package id).
/// </summary>
[ExtensionVerb("sharedverb")]
public sealed class ExtensionIndex : IExtensionIndex
{
    private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
    {
        ["sharedverb alpha"] = new CommandMetadata(
            Name: "AlphaProcessor",
            InvocationName: "sharedverb alpha",
            Aliases: [],
            FullName: "Azclips.Tests.Fixtures.SharedAlpha.AlphaProcessor",
            AssemblyName: "Azclips.Tests.Fixtures.SharedAlpha",
            ProcessorType: typeof(AlphaProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>()),
    };

    public CommandMetadata GetCommandMetadata(string name, ClientType clientType) =>
        clientType == ClientType.CLI && s_cliTable.TryGetValue(name, out var md)
            ? md
            : throw new KeyNotFoundException(name);

    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType) =>
        clientType == ClientType.CLI ? s_cliTable : new Dictionary<string, CommandMetadata>();

    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) =>
        clientType == ClientType.CLI ? s_cliTable.Values : Array.Empty<CommandMetadata>();

    public IEnumerable<string> GetCommandKeys(ClientType clientType) =>
        clientType == ClientType.CLI ? s_cliTable.Keys : Array.Empty<string>();
}

public sealed class AlphaProcessor : IProcessor
{
    private ProcessorContext _context = null!;

    public IProcessor Init(ProcessorContext context)
    {
        _context = context;
        return this;
    }

    public Task ProcessAsync() => _context.OutputHandler.WriteOutput("alpha-payload", CancellationToken.None);
}
