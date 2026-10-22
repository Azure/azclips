// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;

#if INSTALLED_FIXTURE
namespace Azclips.Tests.Fixtures.InstalledExecution;
#else
namespace Azclips.Tests.Fixtures.InstallationExecution;
#endif

// Loaded in isolated contexts to distinguish processor provenance for equal-version copies.
[ExtensionVerb("probe")]
public sealed class ExtensionIndex : IExtensionIndex
{
    private static readonly CommandMetadata s_command = new(
        "Run", "probe run", [], typeof(InstallationProbeProcessor).FullName!,
        typeof(ExtensionIndex).Assembly.GetName().Name!, typeof(InstallationProbeProcessor),
        new Dictionary<string, ParameterMetadata>(), new Dictionary<string, string>());

    private static readonly IReadOnlyDictionary<string, CommandMetadata> s_commands =
        new Dictionary<string, CommandMetadata>
        {
            [s_command.InvocationName] = s_command,
#if INSTALLED_FIXTURE
            ["probe aaa"] = s_command with { InvocationName = "probe aaa" },
            ["probe zzz"] = s_command with { InvocationName = "probe zzz" },
#endif
        };

    public CommandMetadata GetCommandMetadata(string name, ClientType clientType)
        => s_commands[name];
    public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
        => s_commands;
    public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => s_commands.Values;
    public IEnumerable<string> GetCommandKeys(ClientType clientType) => s_commands.Keys;
}

public sealed class InstallationProbeProcessor : IProcessor
{
    private ProcessorContext _context = null!;
    public IProcessor Init(ProcessorContext context)
    {
        _context = context;
        return this;
    }

    public Task ProcessAsync() => _context.OutputHandler.WriteOutput(
#if INSTALLED_FIXTURE
        "installed probe executed",
#else
        "bundled probe executed",
#endif
        CancellationToken.None);
}
