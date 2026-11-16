// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// <c>ExtensionIndex</c> wires a flavored argument to a processor property by string, and nothing
/// links the two at compile time: the binder resolves <see cref="ParameterMetadata.Name"/> through
/// reflection, so a rename on either side survives the build and only fails when that one command
/// runs. These walk every registered command of every extension and check the wiring up front.
/// </summary>
public class ExtensionIndexContractTests
{
    /// <summary>
    /// Pre-existing gaps in other extensions, left to their owners rather than folded into an
    /// unrelated change. Entries are matched as prefixes of the problem message; delete one when its
    /// bug is fixed and the guard starts covering that command again.
    /// </summary>
    private static readonly string[] KnownIssues =
    [
        // RestProcessor_Orchestrator implements IProcessor directly and keeps the resolved profile in
        // a *field* named Profile, so the --profile/-Profile that the index injects into every command
        // has no property to bind to: `az rest --profile <name>` fails at bind time.
        "Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex 'rest' [profile]",
        "Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex 'Invoke-AzRestMethod' [Profile]",

        // Existing MsGraph PowerShell aliases exposed when all extension projects became copy-local.
        "Microsoft.Azure.Azclips.Extensions.MsGraph.ExtensionIndex 'Get-AzMsGraphGroup_Get' [ObjectId]: alias 'Id' is documented but absent from ParameterAliases",
        "Microsoft.Azure.Azclips.Extensions.MsGraph.ExtensionIndex 'Get-AzMsGraphGroup_DisplayNameStartsWith' [DisplayNameStartsWith]: alias 'SearchString' is documented but absent from ParameterAliases",
        "Microsoft.Azure.Azclips.Extensions.MsGraph.ExtensionIndex 'Get-AzMsGraphGroupMember_ObjectId' [GroupObjectId]: alias 'Id' is documented but absent from ParameterAliases",
        "Microsoft.Azure.Azclips.Extensions.MsGraph.ExtensionIndex 'Get-AzMsGraphGroupMember_ObjectId' [GroupObjectId]: alias 'ObjectId' is documented but absent from ParameterAliases",
    ];

    private static List<string> Unexpected(IEnumerable<string> problems) =>
        problems.Where(problem => !KnownIssues.Any(problem.StartsWith)).ToList();

    internal static IEnumerable<(string Index, IExtensionIndex Instance)> Indexes()
    {
        var directory = Path.GetDirectoryName(typeof(ExtensionIndexContractTests).Assembly.Location)!;
        foreach (var path in Directory.EnumerateFiles(directory, "Microsoft.Azure.Azclips.Extensions.*.dll"))
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(path);
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            foreach (var type in assembly.GetExportedTypes())
            {
                if (!typeof(IExtensionIndex).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }
                if (type.GetConstructor(Type.EmptyTypes) is not null && Activator.CreateInstance(type) is IExtensionIndex index)
                {
                    yield return (type.FullName!, index);
                }
            }
        }
    }

    private static IEnumerable<(string Index, string Command, CommandMetadata Metadata)> Commands(ClientType clientType)
    {
        foreach (var (index, instance) in Indexes())
        {
            IReadOnlyDictionary<string, CommandMetadata> table;
            try
            {
                table = instance.GetCommandTable(clientType);
            }
            catch (ArgumentException)
            {
                // Extension does not serve this client surface.
                continue;
            }

            foreach (var (command, metadata) in table)
            {
                yield return (index, command, metadata);
            }
        }
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void EveryParameter_ResolvesToASettableProcessorProperty(ClientType clientType)
    {
        var problems = new List<string>();
        var commands = 0;

        foreach (var (index, command, metadata) in Commands(clientType))
        {
            commands++;
            foreach (var (flavoredName, parameter) in metadata.Parameters)
            {
                var property = metadata.ProcessorType.GetProperty(parameter.Name);
                if (property is null)
                {
                    problems.Add($"{index} '{command}' [{flavoredName}]: {metadata.ProcessorType.Name} has no property '{parameter.Name}'");
                }
                else if (!property.CanWrite)
                {
                    problems.Add($"{index} '{command}' [{flavoredName}]: {metadata.ProcessorType.Name}.{parameter.Name} is read-only");
                }
            }
        }

        Assert.True(commands > 0, "no extension indexes were discovered");
        var unexpected = Unexpected(problems);
        Assert.True(unexpected.Count == 0, string.Join(Environment.NewLine, unexpected));
    }

    /// <summary>
    /// <see cref="CommandMetadata.FullName"/> is a hand-written duplicate of the processor's type
    /// name, used for diagnostics and leaf indexing. It drifts silently when a processor is renamed.
    /// </summary>
    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void EveryCommand_DeclaresTheFullNameOfItsProcessorType(ClientType clientType)
    {
        var problems = Commands(clientType)
            .Where(entry => entry.Metadata.FullName != entry.Metadata.ProcessorType.FullName)
            .Select(entry => $"{entry.Index} '{entry.Command}': FullName '{entry.Metadata.FullName}' but ProcessorType is '{entry.Metadata.ProcessorType.FullName}'")
            .ToList();

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// <see cref="ParameterMetadata.Aliases"/> only renders help; the parsers resolve short flags
    /// through <see cref="CommandMetadata.ParameterAliases"/>. An alias listed in one but not the
    /// other prints in help and then fails to bind, or binds without ever being documented.
    /// </summary>
    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void EveryDocumentedAlias_AlsoResolvesInTheParserTable(ClientType clientType)
    {
        var problems = new List<string>();

        foreach (var (index, command, metadata) in Commands(clientType))
        {
            foreach (var (flavoredName, parameter) in metadata.Parameters)
            {
                foreach (var alias in parameter.Aliases)
                {
                    var parserAlias = alias.StartsWith('-') ? alias[1..] : alias;
                    if (!metadata.ParameterAliases.TryGetValue(parserAlias, out var resolved))
                    {
                        problems.Add($"{index} '{command}' [{flavoredName}]: alias '{alias}' is documented but absent from ParameterAliases");
                    }
                    else if (resolved != flavoredName)
                    {
                        problems.Add($"{index} '{command}' [{flavoredName}]: alias '{alias}' resolves to '{resolved}'");
                    }
                }
            }

            foreach (var (alias, target) in metadata.ParameterAliases)
            {
                if (!metadata.Parameters.ContainsKey(target))
                {
                    problems.Add($"{index} '{command}': alias '{alias}' targets unknown parameter '{target}'");
                }
            }
        }

        var unexpected = Unexpected(problems);
        Assert.True(unexpected.Count == 0, string.Join(Environment.NewLine, unexpected));
    }
}
