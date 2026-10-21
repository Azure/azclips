// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Cli.Tests;

// Loaded in isolated contexts to distinguish processor provenance for equal-version copies.
[ExtensionVerb("probe")]
public sealed class ExtensionIndex : IExtensionIndex
{
    private static readonly CommandMetadata s_command = new(
        "Run", "probe run", [], typeof(InstallationProbeProcessor).FullName!,
        typeof(ExtensionIndex).Assembly.GetName().Name!, typeof(InstallationProbeProcessor),
        new Dictionary<string, ParameterMetadata>(), new Dictionary<string, string>());

    public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
        => name == s_command.InvocationName ? s_command : throw new KeyNotFoundException(name);
    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
        => new Dictionary<string, CommandMetadata> { [s_command.InvocationName] = s_command };
    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => [s_command];
    public IEnumerable<string> GetCommandKeys(ClientType clientType) => [s_command.InvocationName];
}

public sealed class InstallationProbeProcessor : IProcessor
{
    private ProcessorContext _context = null!;
    public IProcessor Init(ProcessorContext context)
    {
        _context = context;
        return this;
    }

    public Task ProcessAsync() => _context.OutputHandler.WriteOutput("probe executed", CancellationToken.None);
}
